﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using puck.core.Abstract;
using System.Linq;
using Lucene.Net.Documents;
using puck.core.Attributes;
using puck.core.Constants;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Snowball;
using System.Web;
using puck.core.Base;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using puck.core.State;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace puck.core.Helpers
{
    public class FlattenedObject {
        public BaseModel Model;
        public object[] ParentListAttributes { get; set; }
        public Type Type { get; set; }
        public String Key { get; set; }
        public Object Value {get;set;}
        public Object OriginalValue { get; set; }
        public Object[] Attributes { get; set; }
        public Field.Index FieldIndexSetting{get;set;}
        public Field.Store FieldStoreSetting{get;set;}
        public bool Ignore { get; set; }
        public Analyzer Analyzer { get; set; }
        public String UniqueKey { get; set; }
        public bool KeepValueCasing { get; set; }
        public bool Spatial { get; set; }
        public void Transform(Dictionary<string,object> dictionary=null,List<Type> allowedTransformers=null) {
            if (dictionary == null)
                dictionary = new Dictionary<string, object>();
            if (Attributes == null)
                Attributes = new object[] { };
            if (ParentListAttributes == null)
                ParentListAttributes = new object[] { };

            //lower case keys
            //Key = Key.ToLower();
            
            //find field settings
            var settings = Attributes.Where(x => x.GetType() == typeof(IndexSettings));
            var parentListSettings = ParentListAttributes.Where(x => x.GetType() == typeof(IndexSettings));
            if (!settings.Any() && parentListSettings.Any())
                settings = parentListSettings;
            if (settings.Any())
            {
                var sattr = (IndexSettings)settings.First();
                KeepValueCasing = !sattr.LowerCaseValue;
                Ignore = sattr.Ignore;
                Spatial = sattr.Spatial;
                FieldIndexSetting = sattr.FieldIndexSetting;
                FieldStoreSetting = sattr.FieldStoreSetting;
                if (sattr.Analyzer != null)
                {
                    if (sattr.Analyzer == typeof(SnowballAnalyzer))
                    {
                        Analyzer = new SnowballAnalyzer(Lucene.Net.Util.LuceneVersion.LUCENE_48, "English");
                    }
                    else
                    {
                        try
                        {
                            Analyzer = (Analyzer)Activator.CreateInstance(sattr.Analyzer, Lucene.Net.Util.LuceneVersion.LUCENE_48);
                        }
                        catch (MissingMethodException mmex) {
                            Analyzer = (Analyzer)Activator.CreateInstance(sattr.Analyzer);
                        }
                    }
                }
            }
            else
            {
                KeepValueCasing = true;
                FieldIndexSetting = FieldSettings.FieldIndexSetting;
                FieldStoreSetting = FieldSettings.FieldStoreSetting;
            }

            //apply transforms
            var tattr = Attributes
                .Where(x => x.GetType().GetInterfaces()
                    .Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(I_Property_Transformer<,>))
                ).ToList();
            OriginalValue = Value;
            var task = ObjectDumper.DoTransform(tattr,Type,Model,Key,UniqueKey,Value,dictionary,allowedTransformers:allowedTransformers);
            task.GetAwaiter().GetResult();
            Value = task.Result;
        }
        
    }
    public class ObjectDumper
    {
        public static Dictionary<string,object> ToDictionary(List<FlattenedObject> props){
            var result = new Dictionary<string, object>();
            foreach (var p in props) {
                result.Add(p.Key.ToLower(),p.Value??string.Empty);
            }
            return result;
        }
        public static List<FlattenedObject> Write(object element, int depth,List<Type> allowedTransformers=null)
        {
            ObjectDumper dumper = new ObjectDumper(depth);
            dumper.topElement = element as BaseModel;
            dumper.WriteObject_("","", element,elementParent:element);
            dumper.dict = new Dictionary<string, object>();
            dumper.result.ForEach(x => {
                if (x.Ignore)
                    dumper.result.Remove(x);
                else
                    x.Transform(dictionary:dumper.dict,allowedTransformers:allowedTransformers); 
            });
            return dumper.result;
        }
        public static async Task<object> DoTransform(List<object> attributes,Type valueType, object element, string propertyName, string uniqueKey, object value,Dictionary<string,object> dictionary,List<Type> allowedTransformers=null,bool allowMultiple=false) {
            object result = value;
            object attr = null;
            var transformerAttributes = new List<object>();
            var tattr = attributes
                .Where(x => x.GetType().GetInterfaces()
                    .Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(I_Property_Transformer<,>))
                ).Where(x=>allowedTransformers==null||allowedTransformers.Contains(x.GetType())).ToList();
            if (tattr.Any())//check for custom transform attribute
            {
                //attr = tattr.First();
                if (allowMultiple) {
                    if (FieldSettings.DefaultPropertyTransformers.ContainsKey(valueType))
                    {
                        if (allowedTransformers == null || allowedTransformers.Contains(FieldSettings.DefaultPropertyTransformers[valueType]))
                            tattr.Add(Activator.CreateInstance(FieldSettings.DefaultPropertyTransformers[valueType]));
                    }
                }
            }
            else
            { //check for default transform for type
                if (FieldSettings.DefaultPropertyTransformers.ContainsKey(valueType))
                {
                    if(allowedTransformers==null || allowedTransformers.Contains(FieldSettings.DefaultPropertyTransformers[valueType]))
                        tattr.Add(Activator.CreateInstance(FieldSettings.DefaultPropertyTransformers[valueType]));
                }
            }
            //transform if possible
            if (tattr.Count > 0)
            {
                var n = allowMultiple ? tattr.Count : 1;
                for (var i = 0; i < n; i++)
                {
                    attr = tattr[i];
                    if (attr != null)
                    {
                        using (var scope = PuckCache.ServiceProvider.CreateScope())
                        {
                            var configureMethod = attr.GetType().GetMethod("Configure");
                            if (configureMethod != null)
                            {
                                var parameterInfos = configureMethod.GetParameters();
                                var parameters = new List<object>();
                                foreach (var paramInfo in parameterInfos)
                                {
                                    var parameter = scope.ServiceProvider.GetService(paramInfo.ParameterType);
                                    parameters.Add(parameter);
                                }
                                configureMethod.Invoke(attr, parameters.ToArray());
                            }
                            var task = (Task)attr.GetType().GetMethod("Transform").Invoke(attr, new[] { element, propertyName, uniqueKey, result, dictionary });
                            await task.ConfigureAwait(false);
                            var resultProperty = task.GetType().GetProperty("Result");
                            var newValue = resultProperty.GetValue(task);
                            result = newValue;
                        }
                    }
                }
            }
            return result;
        }
        public static async Task Transform(object element, int depth,List<Type> allowedTransformers=null)
        {
            ObjectDumper dumper = new ObjectDumper(depth);
            dumper.topElement = element as BaseModel;
            dumper.dict = new Dictionary<string, object>();
            dumper.allowedTransformers = allowedTransformers;
            var attributes = element.GetType().GetCustomAttributes(true).ToList();
            element = await DoTransform(attributes,element.GetType(),element,"","",element,dumper.dict,dumper.allowedTransformers,allowMultiple:true);
            //transform the rest of the model
            await dumper.Transform("","", element);
        }
        public static void BindImages(object element, int depth,IFormFileCollection files)
        {
            ObjectDumper dumper = new ObjectDumper(depth);
            dumper.files = files;
            dumper.DoBindImages("","", element);            
        }

        int level;
        int depth;
        BaseModel topElement;
        Dictionary<string, object> dict;
        List<Type> allowedTransformers = null;
        List<string> fieldsToIgnore = new List<string> {"References"};
        public List<FlattenedObject> result = new List<FlattenedObject>();
        IFormFileCollection files;
        private ObjectDumper(int depth)
        {
            this.depth = depth;
        }

        private void DoBindImages(string prefix,string ukey,object element)
        {
            if (element == null || element is ValueType || element is string)
            {
                
            }
            else
            {
                IEnumerable enumerableElement = element as IEnumerable;
                if (enumerableElement != null)
                {
                    var i = 0;
                    if (ukey.EndsWith("."))
                        ukey=ukey.Remove(ukey.Length - 1);
                    foreach (object item in enumerableElement)
                    {
                        if (item is IEnumerable && !(item is string))
                        {
                            if (level < depth)
                            {
                                level++;
                                DoBindImages(prefix, ukey + "[" + i +"].", item);
                                level--;
                            }
                        }
                        else
                        {
                            DoBindImages(prefix, ukey + "[" + i + "].", item);
                        }
                        i++;
                    }
                }
                else
                {
                    PropertyInfo[] properties = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    bool propWritten = false;
                    foreach (PropertyInfo p in properties)
                    {
                        if (p != null)
                        {
                            if (propWritten)
                            {

                            }
                            else
                            {
                                propWritten = true;
                            }
                            Type t = p.PropertyType;
                            if (t.IsValueType || t == typeof(string))
                            {
                                
                            }
                            else
                            {
                                if (level < depth && !(t.IsValueType || t == typeof(string)))
                                {
                                    if (files!=null && files[ukey + p.Name] != null)
                                    {
                                        var pf = files[ukey + p.Name];
                                        if (pf != null){
                                            p.SetValue(element, pf, null);
                                        }
                                    }
                                    object value = p.GetValue(element, null);
                                    if (value != null)
                                    {
                                        level++;
                                        DoBindImages(prefix+p.Name + ".",ukey+p.Name+".", value);
                                        level--;
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        private async Task Transform(string prefix,string ukey, object element)
        {
            if (element == null || element is ValueType || element is string)
            {
                
            }
            else
            {
                IEnumerable enumerableElement = element as IEnumerable;
                if (enumerableElement != null)
                {
                    var i = 0;
                    if (ukey.EndsWith("."))
                        ukey=ukey.Remove(ukey.Length-1);
                    
                    foreach (object item in enumerableElement)
                    {
                        if (item is IEnumerable && !(item is string))
                        {
                            if (level < depth)
                            {
                                level++;
                                await Transform(prefix, ukey + "[" + i + "].", item);
                                level--;
                            }
                        }
                        else
                        {
                            var _item = item;
                            if (PuckCache.TransformListElements && _item != null && !( _item is string) && !(_item is ValueType)) {
                                //transform
                                var attributes = _item.GetType().GetCustomAttributes(true).ToList();
                                var newValue = await DoTransform(attributes,_item.GetType(), topElement, prefix.TrimEnd('.'), ukey.TrimEnd('.') + "[" + i + "]", _item, dict, allowedTransformers: allowedTransformers,allowMultiple:true);
                                if (newValue != _item)
                                    _item = newValue;
                            }
                            if (item != null && level < depth)
                            {
                                level++;
                                await Transform(prefix, ukey + "[" + i + "].", item);
                                level--;
                            }
                        }
                        i++;
                    }
                }
                else
                {
                    PropertyInfo[] properties = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    bool propWritten = false;
                    foreach (PropertyInfo p in properties)
                    {
                        if (p != null)
                        {
                            if (propWritten)
                            {

                            }
                            else
                            {
                                propWritten = true;
                            }
                            Type t = p.PropertyType;
                            if (t.IsValueType || t == typeof(string))
                            {
                                
                            }
                            else
                            {
                                if (level < depth && !(t.IsValueType || t == typeof(string)))
                                {
                                    object value = p.GetValue(element, null);
                                    string propertyName = prefix + p.Name;
                                    if (value != null && !fieldsToIgnore.Contains(propertyName.TrimEnd('.')))
                                    {
                                        //transform
                                        var attributes = p.GetCustomAttributes(true).ToList();
                                        attributes.AddRange(p.PropertyType.GetCustomAttributes(true));
                                        var newValue = await DoTransform(attributes,t,topElement,propertyName,ukey+p.Name,value,dict,allowedTransformers:allowedTransformers,allowMultiple:true);
                                        if(newValue!=value && (newValue == null || newValue.GetType() == value.GetType()))
                                            p.SetValue(element, newValue, null);
                                        if (newValue != null)
                                        {
                                            level++;
                                            await Transform(propertyName + ".", ukey + p.Name + ".", newValue);
                                            level--;
                                        }
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        private void WriteObject_(string prefix,string ukey, object element,PropertyInfo listProperty=null,object elementParent=null)
        {
            var elementType = element?.GetType();
            if (element == null) { 
            
            }else if ((element is ValueType || element is string) && !(elementType.IsGenericType && elementType.GetGenericTypeDefinition()==typeof(KeyValuePair<,>)))
            {
                var fo = new FlattenedObject
                {
                    UniqueKey = ukey.TrimEnd('.'),
                    Model = topElement,
                    Key = prefix.TrimEnd('.'),
                    Value = element,
                    Type = element == null ? (element is string ? typeof(String) : null) : element.GetType(),
                    ParentListAttributes = listProperty == null ? new object[] { } : listProperty.GetCustomAttributes(true)
                };
                result.Add(fo);
            }
            else
            {
                IEnumerable enumerableElement = element as IEnumerable;
                if (enumerableElement != null)
                {
                    var i = 0;
                    if (ukey.EndsWith("."))
                        ukey = ukey.Remove(ukey.Length - 1);
                    var propName = prefix.TrimEnd('.');
                    if (propName.IndexOf(".") > -1)
                        propName = propName.Substring(propName.LastIndexOf(".") + 1);
                    var listProp = elementParent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name.Equals(propName));
                    foreach (object item in enumerableElement)
                    {
                        if (item is IEnumerable && !(item is string))
                        {
                            if (level < depth)
                            {
                                level++;
                                WriteObject_(prefix, ukey + "[" + i + "].", item, listProperty: listProp, elementParent: element);
                                level--;
                            }
                        }
                        else
                        {
                            WriteObject_(prefix, ukey + "[" + i + "].", item, listProperty: listProp, elementParent: element);
                        }
                        i++;
                    }
                }
                else
                {
                    PropertyInfo[] properties = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    bool propWritten = false;
                    foreach (PropertyInfo p in properties)
                    {
                        if (p != null)
                        {
                            if (propWritten)
                            {

                            }
                            else
                            {
                                propWritten = true;
                            }
                            Type t = p.PropertyType;
                            if (t.IsValueType || t == typeof(string))
                            {
                                var value = p.GetValue(element, null);
                                if (value != null)
                                {
                                    result.Add(new FlattenedObject
                                    {
                                        UniqueKey = ukey + p.Name,
                                        Model = topElement,
                                        Key = prefix + p.Name,
                                        Value = value,
                                        Type = ApiHelper.GetType(p.PropertyType.AssemblyQualifiedName),
                                        Attributes = p.GetCustomAttributes(true)
                                    });
                                }
                            }
                            else
                            {
                                if (level < depth && !(t.IsValueType || t == typeof(string)))
                                {
                                    object value = p.GetValue(element, null);
                                    if (value != null)
                                    {
                                        level++;
                                        WriteObject_(prefix + p.Name + ".", ukey + p.Name + ".", value, elementParent: element);
                                        level--;
                                    }
                                }
                            }
                        }
                    }

                }
            }
        }

        private void WriteObject(string prefix, object element)
        {
            if (element == null || element is ValueType || element is string)
            {
                var fo = new FlattenedObject
                {
                    Key = prefix
                    ,
                    Value = element == null
                    ,
                    Type = element == null ? (element is string ? typeof(String) : null) : element.GetType()
                };
                result.Add(fo);
            }
            else
            {
                IEnumerable enumerableElement = element as IEnumerable;
                if (enumerableElement != null)
                {
                    foreach (object item in enumerableElement)
                    {
                        if (item is IEnumerable && !(item is string))
                        {
                            if (level < depth)
                            {
                                level++;
                                WriteObject(prefix, item);
                                level--;
                            }
                        }
                        else
                        {
                            WriteObject(prefix, item);
                        }
                    }
                }
                else
                {
                    MemberInfo[] members = element.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance);
                    bool propWritten = false;
                    foreach (MemberInfo m in members)
                    {
                        FieldInfo f = m as FieldInfo;
                        PropertyInfo p = m as PropertyInfo;
                        if (f != null || p != null)
                        {
                            if (propWritten)
                            {
                                
                            }
                            else
                            {
                                propWritten = true;
                            }
                            Type t = f != null ? f.FieldType : p.PropertyType;
                            if (t.IsValueType || t == typeof(string))
                            {
                                result.Add(new FlattenedObject
                                {
                                    Key = prefix+m.Name
                                    ,Value = f != null ? f.GetValue(element) : p.GetValue(element, null)!=null?p.GetValue(element, null):null
                                    ,Type = f != null ? ApiHelper.GetType(f.FieldType.AssemblyQualifiedName) : ApiHelper.GetType(p.PropertyType.AssemblyQualifiedName)
                                    ,Attributes = m.GetCustomAttributes(true)
                                });                                
                            }
                            else
                            {
                                if (typeof(IEnumerable).IsAssignableFrom(t))
                                {
                                    //"..."
                                }
                                else
                                {
                                    //"{ }"
                                }
                            }
                        }
                    }
                    if (level < depth)
                    {
                        foreach (MemberInfo m in members)
                        {
                            FieldInfo f = m as FieldInfo;
                            PropertyInfo p = m as PropertyInfo;
                            if (f != null || p != null)
                            {
                                Type t = f != null ? f.FieldType : p.PropertyType;
                                if (!(t.IsValueType || t == typeof(string)))
                                {
                                    object value = f != null ? f.GetValue(element) : p.GetValue(element, null);
                                    if (value != null)
                                    {
                                        level++;
                                        WriteObject(m.Name + ".", value);
                                        level--;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public static int SetPropertyValuesMaxDepth = 50;
        public static void SetPropertyValues(object obj,bool onlyPopulateListEditorLists=false,int depth=1,bool setNullableFields=false)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties();

            foreach (PropertyInfo property in properties)
            {
                if (IsPropertyACollection(property))
                {
                    var uiHintAttribute = property.GetCustomAttribute<UIHintAttribute>();
                    var hasListEditor = false;
                    if (uiHintAttribute != null) {
                        hasListEditor = uiHintAttribute.UIHint.ToLower() == "listeditor";
                    }
                    Type propType = property.PropertyType;

                    var subObject = Activator.CreateInstance(propType);

                    if (propType.IsGenericType && propType.GetGenericArguments().Length == 1
                        /*&&
                        propType.GetGenericTypeDefinition()
                        == typeof(IList<>)*/)
                    {
                        if (onlyPopulateListEditorLists && hasListEditor || !onlyPopulateListEditorLists)
                        {
                            Type itemType = propType.GetGenericArguments()[0];
                            object listItem;
                            if (itemType == typeof(string))
                                listItem = " ";
                            else if (itemType == typeof(Single?))
                            {
                                listItem = (Single?)0f;
                            }
                            else if (itemType == typeof(Char?))
                            {
                                listItem = (Char?)' ';
                            }
                            else if (itemType == typeof(Decimal?))
                            {
                                listItem = (Decimal?)0m;
                            }
                            else if (itemType == typeof(Double?))
                            {
                                listItem = (Double?)0d;
                            }
                            else if (itemType == typeof(Int16?))
                            {
                                listItem = (Int16?)0;
                            }
                            else if (itemType == typeof(Int32?))
                            {
                                listItem = (Int32?)0;
                            }
                            else if (itemType == typeof(Int64?))
                            {
                                listItem = (Int64?)0;
                            }
                            else if (itemType == typeof(bool?))
                            {
                                listItem = (bool?)false;
                            }
                            else
                                listItem = Activator.CreateInstance(itemType);
                            subObject.GetType().GetMethod("Add").Invoke(subObject, new[] { listItem });
                            if (depth < SetPropertyValuesMaxDepth && listItem != null)
                                SetPropertyValues(listItem, onlyPopulateListEditorLists: onlyPopulateListEditorLists, depth: depth + 1, setNullableFields: setNullableFields);
                        }
                        property.SetValue(obj, subObject, null);
                    }
                    else if (propType.GetGenericArguments().Length > 1) {
                        if (propType.GetGenericArguments().Length == 2) {
                            //likely a dictionary
                            Type keyType = propType.GetGenericArguments()[0];
                            //TODO, handle dictionaries
                        }
                    }
                }
                else
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    Type propType = property.PropertyType;
                    try
                    {
                        var subObject = Activator.CreateInstance(propType);
                        if(depth<SetPropertyValuesMaxDepth)
                            SetPropertyValues(subObject,onlyPopulateListEditorLists:onlyPopulateListEditorLists,depth:depth+1,setNullableFields:setNullableFields);
                        property.SetValue(obj, subObject, null);
                    }
                    catch (Exception ex) { }
                }
                else if (property.PropertyType == typeof(string))
                {
                    try
                    {
                        property.SetValue(obj, "", null);
                    }
                    catch (System.ArgumentException ex) {/*no setter - likely the read only Url property*/ };
                }
                else if (setNullableFields && property.PropertyType == typeof(Single?))
                {
                    property.SetValue(obj, (Single?)0f, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(char?))
                {
                    property.SetValue(obj, (Char?)' ', null);
                }
                else if (setNullableFields && property.PropertyType == typeof(Decimal?))
                {
                    property.SetValue(obj, (Decimal?)0m, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(Double?))
                {
                    property.SetValue(obj, (Double?)0d, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(Int16?))
                {
                    property.SetValue(obj, (Int16?)0, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(Int32?))
                {
                    property.SetValue(obj, (Int32?)0, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(Int64?))
                {
                    property.SetValue(obj, (Int64?)0, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(bool?))
                {
                    property.SetValue(obj, (bool?)false, null);
                }
                else if (setNullableFields && property.PropertyType == typeof(DateTime?))
                {
                    property.SetValue(obj, (DateTime?)DateTime.Now, null);
                }

                /*
                else if (property.PropertyType == typeof(DateTime))
                {
                    property.SetValue(obj, DateTime.Today, null);
                }
                else if (property.PropertyType == typeof(int))
                {
                    property.SetValue(obj, 0, null);
                }
                else if (property.PropertyType == typeof(decimal))
                {
                    property.SetValue(obj, 0, null);
                }*/
            }
        }

        public static bool IsPropertyACollection(PropertyInfo property)
        {
            return property.PropertyType.GetInterface(typeof(IEnumerable<>).FullName) != null && property.PropertyType != typeof(string);
        }
    }
}
