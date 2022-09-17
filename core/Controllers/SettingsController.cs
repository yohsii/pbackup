﻿using System;
using System.Collections.Generic;
using System.Linq;
using puck.core.Abstract;
using puck.core.Models;
using puck.core.Constants;
using puck.core.Entities;
using puck.core.Helpers;
using puck.core.Filters;
using Newtonsoft.Json;
using puck.core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace puck.core.Controllers
{
    [Area("puck")]
    //[SetPuckCulture]
    [Authorize(Roles=PuckRoles.Settings,AuthenticationSchemes =Mvc.AuthenticationScheme)]
    public class SettingsController : BaseController
    {
        I_Content_Indexer indexer;
        I_Content_Searcher searcher;
        I_Log log;
        I_Puck_Repository repo;
        I_Content_Service contentService;
        I_Api_Helper apiHelper;
        IMemoryCache cache;
        public SettingsController(I_Api_Helper ah,I_Content_Service cs,I_Content_Indexer i, I_Content_Searcher s, I_Log l, I_Puck_Repository r,IMemoryCache cache) {
            this.apiHelper = ah;
            this.contentService = cs;
            this.indexer = i;
            this.searcher = s;
            this.log = l;
            this.repo = r;
            this.cache = cache;
        }

        public JsonResult DeleteParameters(string key) {
            bool success = false;
            string message = "";
            try
            {
                var metas = repo.GetPuckMeta().Where(x => x.Name == DBNames.EditorSettings && x.Key == key).ToList();
                var meta = metas.FirstOrDefault();
                metas.ForEach(x=>repo.DeletePuckMeta(x));

                //clear cached values
                var cachePrefix = "editor_settings_";
                var cacheKey = cachePrefix + key;
                cache.Remove(cacheKey);
                cache.Remove("null_" + cacheKey);

                var instruction = new PuckInstruction()
                {
                    Count = 2,
                    ServerName = ApiHelper.ServerName(),
                    InstructionDetail = $"{cacheKey},{"null_" + cacheKey}",
                    InstructionKey = InstructionKeys.RemoveFromCache
                };
                repo.AddPuckInstruction(instruction);

                repo.SaveChanges();
                if (meta != null)
                {
                    var keyParts = key.Split(new char[] { ':' });
                    var typeSettings = ApiHelper.EditorSettingTypes().FirstOrDefault(x => x.FullName == keyParts[0]);
                    object model = JsonConvert.DeserializeObject(meta.Value, typeSettings);
                    ApiHelper.OnAfterSettingsDelete(this, new puck.core.Events.AfterEditorSettingsDeleteEventArgs
                    {
                        Setting = (I_Puck_Editor_Settings)model
                        ,SettingsTypeFullName = keyParts[0]
                        ,ModelTypeName = keyParts[1]
                        ,PropertyName = keyParts[2]
                    });
                    
                }
                success = true;
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
                log.Log(ex);
            }
            return Json(new { success = success, message = message });
        }

        public ActionResult EditParameters(string settingsType,string modelType,string propertyName) {
            string key = string.Concat(settingsType, ":", modelType, ":", propertyName);
            var typeModel = ApiHelper.GetTypeFromName(modelType);
            //var typeSettings = Type.GetType(settingsType);
            var typeSettings = ApiHelper.EditorSettingTypes().FirstOrDefault(x=> x.FullName==settingsType);
            var meta = repo.GetPuckMeta().Where(x=>x.Name==DBNames.EditorSettings && x.Key == key).FirstOrDefault();
            object model = null;
            if (meta != null) {
                try {
                    model = JsonConvert.DeserializeObject(meta.Value, typeSettings);
                }
                catch (Exception ex) {
                    log.Log(ex);
                }
            }
            if (model == null) {
                model = Activator.CreateInstance(typeSettings);
            }
            ViewBag.ShouldBindListEditor = true;
            ViewBag.IsPrepopulated = false;
            ViewBag.Level0Type = typeSettings;
            return View(model);
        }

        [HttpPost]
        public async Task<JsonResult> EditParameters(string puck_settingsType,string puck_modelType,string puck_propertyName,IFormCollection fc) {
            string key = string.Concat(puck_settingsType, ":", puck_modelType, ":", puck_propertyName);
            //var targetType = Type.GetType(puck_settingsType);
            var targetType = ApiHelper.EditorSettingTypes().FirstOrDefault(x=>x.FullName==puck_settingsType);
            var model = Activator.CreateInstance(targetType);
            bool success = false;
            string message = "";
            try
            {
                if (await this.TryUpdateModelAsync(model, targetType, ""))
                {
                    PuckMeta settingsMeta = null;
                    settingsMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.EditorSettings && x.Key == key).FirstOrDefault();
                    if (settingsMeta != null)
                    {
                        settingsMeta.Value = JsonConvert.SerializeObject(model);
                    }
                    else
                    {
                        settingsMeta = new PuckMeta();
                        settingsMeta.Name = DBNames.EditorSettings;
                        settingsMeta.Key = key;
                        settingsMeta.Value = JsonConvert.SerializeObject(model);
                        repo.AddPuckMeta(settingsMeta);
                    }
                    
                    //clear cached values
                    var cachePrefix = "editor_settings_";
                    var cacheKey = cachePrefix + key;
                    cache.Remove(cacheKey);
                    cache.Remove("null_"+cacheKey);

                    var instruction = new PuckInstruction() {
                        Count = 2,
                        ServerName = ApiHelper.ServerName(),
                        InstructionDetail = $"{cacheKey},{"null_"+cacheKey}",
                        InstructionKey = InstructionKeys.RemoveFromCache
                    };
                    repo.AddPuckInstruction(instruction);

                    repo.SaveChanges();

                    ApiHelper.OnAfterSettingsSave(this, new puck.core.Events.AfterEditorSettingsSaveEventArgs { 
                        Setting = (I_Puck_Editor_Settings)model
                        ,SettingsTypeFullName = puck_settingsType
                        ,ModelTypeName = puck_modelType
                        ,PropertyName = puck_propertyName
                    });
                    success = true;
                }
                else {
                    success = false;
                    message = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
                }
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
                log.Log(ex);
            }
            return Json(new { success = success, message = message });
        }

        public ActionResult Languages() {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var languages = meta.Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.Languages).ToList().OrderBy(x=>x.Dt??DateTime.Now).Select(x => x.Value).ToList();
            model.Languages = languages;

            return View(model);
        }

        [HttpPost]
        public JsonResult Languages(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //languages
                if (model.Languages != null && model.Languages.Count > 0)
                {
                    var metaLanguages = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.Languages).ToList();
                    if (metaLanguages.Count > 0)
                    {
                        metaLanguages.ForEach(x =>
                        {
                            repo.DeletePuckMeta(x);
                        });
                    }
                    var i = 0;
                    foreach(var x in model.Languages)
                    {
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.Settings;
                        newMeta.Key = DBKeys.Languages;
                        newMeta.Value = x;
                        newMeta.Dt = DateTime.Now.AddMinutes(i+1);
                        repo.AddPuckMeta(newMeta);
                        i++;
                    }
                }
                else {
                    var metaLanguages = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.Languages).ToList();
                    if (metaLanguages.Count > 0)
                    {
                        metaLanguages.ForEach(x =>
                        {
                            repo.DeletePuckMeta(x);
                        });
                    }
                }
                
                repo.SaveChanges();
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }
        public ActionResult Redirects()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var redirects = meta.Where(x => x.Name == DBNames.Redirect301 || x.Name == DBNames.Redirect302).ToList()
                .Select(x => new KeyValuePair<string, string>(x.Name + x.Key, x.Value)).ToDictionary(x => x.Key, x => x.Value);
            
            model.Redirect = redirects;
            return View(model);
        }

        [HttpPost]
        public JsonResult Redirects(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //redirects
                //if (model.Redirect != null && model.Redirect.Count > 0)
                //{
                //    var redirectMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.Redirect301 || x.Name == DBNames.Redirect302).ToList();
                //    redirectMeta.ForEach(x => {
                //        repo.DeleteMeta(x);
                //    });
                //    //count of 1 and key/value of null indicates delete only so inserts are skipped
                //    if (!(model.Redirect.Count == 1 && string.IsNullOrEmpty(model.Redirect.First().Key)))
                //    {
                //        model.Redirect.ToList().ForEach(x =>
                //        {
                //            var newMeta = new PuckMeta();
                //            newMeta.Name = x.Key.StartsWith(DBNames.Redirect301) ? DBNames.Redirect301 : DBNames.Redirect302;
                //            newMeta.Key = x.Key.Substring(newMeta.Name.Length);
                //            newMeta.Value = x.Value;
                //            repo.AddMeta(newMeta);
                //        });
                //    }
                //}
                
                //repo.SaveChanges();
                StateHelper.UpdateRedirectMappings(true);
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }
        public ActionResult FieldGroups()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var fieldGroups = meta.Where(x => x.Name.StartsWith(DBNames.FieldGroups)).ToList();
            model.TypeGroupField = new List<string>();

            fieldGroups.ForEach(x => {
                string typeName = x.Name.Replace(DBNames.FieldGroups, "");
                string groupName = x.Key;
                string FieldName = x.Value;
                model.TypeGroupField.Add(string.Concat(typeName, ":", groupName, ":", FieldName));
            });
            return View(model);
        }
        [HttpPost]
        public JsonResult FieldGroups(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //fieldgroup
                if (model.TypeGroupField != null && model.TypeGroupField.Count > 0)
                {
                    foreach (var mod in apiHelper.AllModels(true))
                    {
                        var fieldGroupMeta = repo.GetPuckMeta().Where(x => x.Name.StartsWith(DBNames.FieldGroups + mod.Name)).ToList();
                        fieldGroupMeta.ForEach(x =>
                        {
                            repo.DeletePuckMeta(x);
                        });
                    }
                    model.TypeGroupField.ForEach(x =>
                    {
                        var values = x.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.FieldGroups + values[0];
                        newMeta.Key = values[1];
                        newMeta.Value = values[2];
                        repo.AddPuckMeta(newMeta);
                    });
                }
                else {
                    var fieldGroupMeta = repo.GetPuckMeta().Where(x => x.Name.StartsWith(DBNames.FieldGroups)).ToList();
                    fieldGroupMeta.ForEach(x =>
                    {
                        repo.DeletePuckMeta(x);
                    });
                }

                var modelTypes = apiHelper.Models();
                string cacheKeys = "";
                foreach (var modelType in modelTypes)
                {
                    string cacheKey = "fieldGroups_" + modelType.Name;
                    cacheKeys += cacheKey + ",";
                    cache.Remove(cacheKey);
                }
                cacheKeys = cacheKeys.TrimEnd(',');

                var instruction = new PuckInstruction()
                {
                    Count = modelTypes.Count,
                    ServerName = ApiHelper.ServerName(),
                    InstructionDetail = cacheKeys,
                    InstructionKey = InstructionKeys.RemoveFromCache
                };
                repo.AddPuckInstruction(instruction);

                repo.SaveChanges();
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }
        public ActionResult AllowedTemplates()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var typeAllowedTemplates = meta
                .Where(x => x.Name == DBNames.TypeAllowedTemplates)
                .ToList()
                .OrderBy(x=>x.Dt??DateTime.Now)
                .Select(x => x.Key + ":" + x.Value)
                .ToList();
            model.TypeAllowedTemplates = typeAllowedTemplates;
            return View(model);
        }
        // POST: /puck/Settings/Edit/5
        [HttpPost]
        public JsonResult AllowedTemplates(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //typeallowedtemplates
                var i = 0;
                if (model.TypeAllowedTemplates != null && model.TypeAllowedTemplates.Count > 0)
                {
                    var typeAllowedTemplatesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTemplates).ToList();
                    
                    var metaPosted = new List<PuckMeta>();
                    model.TypeAllowedTemplates.ForEach(x =>
                    {
                        var dt = DateTime.Now.AddMinutes(i+1);
                        var values = x.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.TypeAllowedTemplates;
                        newMeta.Key = values[0];
                        newMeta.Value = values[1];
                        newMeta.Dt = dt;
                        metaPosted.Add(newMeta);
                        var existingMeta = typeAllowedTemplatesMeta.FirstOrDefault(m => m.Name == newMeta.Name && m.Key == newMeta.Key && m.Value == newMeta.Value);
                        if (existingMeta == null)
                        {
                            repo.AddPuckMeta(newMeta);
                        }
                        else {
                            existingMeta.Dt = dt;
                        }
                        i++;
                    });

                    typeAllowedTemplatesMeta.ForEach(x =>
                    {
                        if (!metaPosted.Any(p => p.Name == x.Name && p.Key == x.Key && p.Value == x.Value)) {
                            repo.DeletePuckMeta(x);
                        }
                    });
                }
                else {
                    var typeAllowedTemplatesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTemplates).ToList();
                    typeAllowedTemplatesMeta.ForEach(x =>
                    {
                        repo.DeletePuckMeta(x);
                    });
                }

                var modelTypes = apiHelper.Models();
                string cacheKeys = "";
                foreach (var modelType in modelTypes) {
                    string cacheKey = "allowedViews_"+modelType.Name;
                    cacheKeys += cacheKey+",";
                    cache.Remove(cacheKey);
                }
                cacheKeys = cacheKeys.TrimEnd(',');
                
                var instruction = new PuckInstruction()
                {
                    Count = modelTypes.Count,
                    ServerName = ApiHelper.ServerName(),
                    InstructionDetail = cacheKeys,
                    InstructionKey = InstructionKeys.RemoveFromCache
                };
                repo.AddPuckInstruction(instruction);

                repo.SaveChanges();
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }
        public ActionResult AllowedModels()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var typeAllowedTypes = meta
                .Where(x => x.Name == DBNames.TypeAllowedTypes)
                .ToList()
                .OrderBy(x => x.Dt ?? DateTime.Now)
                .Select(x => x.Key + ":" + x.Value)
                .ToList();
            model.TypeAllowedTypes = typeAllowedTypes;
            return View(model);
        }
        [HttpPost]
        public JsonResult AllowedModels(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //typeallowedtypes
                var i = 0;
                if (model.TypeAllowedTypes != null && model.TypeAllowedTypes.Count > 0)
                {
                    var typeAllowedTypesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTypes).ToList();
                    
                    var metaPosted = new List<PuckMeta>();
                    model.TypeAllowedTypes.ForEach(x =>
                    {
                        var dt = DateTime.Now.AddMinutes(i+1);
                        var values = x.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.TypeAllowedTypes;
                        newMeta.Key = values[0];
                        newMeta.Value = values[1];
                        newMeta.Dt = dt;
                        metaPosted.Add(newMeta);
                        var existingMeta = typeAllowedTypesMeta.FirstOrDefault(m => m.Name == newMeta.Name && m.Key == newMeta.Key && m.Value == newMeta.Value);
                        if (existingMeta == null)
                        {//only add meta that isn't already in db
                            repo.AddPuckMeta(newMeta);
                        }
                        else {
                            existingMeta.Dt = dt;
                        }
                        i++;
                    });

                    typeAllowedTypesMeta.ForEach(x =>
                    {
                        //only delete meta that's in db but wasn't posted back with the model
                        if (!metaPosted.Any(p => p.Name == x.Name && p.Key == x.Key && p.Value == x.Value))
                            repo.DeletePuckMeta(x);
                    });

                }
                else {
                    var typeAllowedTypesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTypes).ToList();
                    typeAllowedTypesMeta.ForEach(x => {
                        repo.DeletePuckMeta(x);
                    });
                }

                repo.SaveChanges();
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }
        public ActionResult EditorParameters()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var editorParameters = meta.Where(x => x.Name == DBNames.EditorSettings).Select(x => x.Key).ToList();
            model.EditorParameters = editorParameters;
            return View(model);
        }

        public ActionResult CachePolicy()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var cachePolicy = meta.Where(x => x.Name == DBNames.CachePolicy).Select(x => x.Key + ":" + x.Value).ToList();
            model.CachePolicy = cachePolicy;
            return View(model);
        }

        [HttpPost]
        public JsonResult CachePolicy(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                //cachepolicy
                if (model.CachePolicy == null)
                    model.CachePolicy = new List<string>();
                var cacheTypes = new List<string>();
                if (model.CachePolicy.Count > 0)
                {
                    foreach (var entry in model.CachePolicy)
                    {
                        var values = entry.Split(new char[] { ':' });
                        var type = values[0];
                        cacheTypes.Add(type);
                        var minutes = values[1];
                        int min;
                        if (!int.TryParse(minutes, out min))
                            throw new Exception("cache policy minutes not int for type:" + type);
                        var varyByQs = string.Join(",",(values[2]??"").Split(",",StringSplitOptions.RemoveEmptyEntries).Where(x=>!string.IsNullOrEmpty(x) && !string.IsNullOrWhiteSpace(x)).Distinct()) ?? "";
                        var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.CachePolicy && x.Key.ToLower().Equals(type.ToLower())).FirstOrDefault();
                        if (meta != null)
                        {
                            meta.Value = minutes + ":" +varyByQs;
                        }
                        else
                        {
                            meta = new PuckMeta() { Name = DBNames.CachePolicy, Key = type, Value = minutes + ":" + varyByQs };
                            repo.AddPuckMeta(meta);
                        }
                    }
                }
                //delete unset
                repo.GetPuckMeta().Where(x => x.Name == DBNames.CachePolicy && !cacheTypes.Contains(x.Key)).ToList().ForEach(x => repo.DeletePuckMeta(x));

                repo.SaveChanges();
                StateHelper.UpdateCacheMappings(true);
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg });
        }

        public ActionResult OrphanedModels()
        {
            var model = new Settings();
            
            return View(model);
        }

        
        [HttpPost]
        public JsonResult OrphanedModels(Settings model)
        {
            string msg = "";
            bool success = false;
            int affected = 0;
            try
            {
                //orphan types
                if (model.Orphans != null && model.Orphans.Count > 0)
                {
                    foreach (var entry in model.Orphans)
                    {
                        var t1 = entry.Key;
                        var t2 = entry.Value;
                        affected = contentService.RenameOrphaned2(t1, t2);
                    }
                }
                repo.SaveChanges();
                
                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success = success, message = msg,affected=affected });
        }

        public ActionResult Edit()
        {
            var model = new Settings();
            var meta = repo.GetPuckMeta();

            var defaultLanguage = meta.Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.DefaultLanguage).FirstOrDefault();
            var enableLocalePrefix = meta.Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.EnableLocalePrefix).FirstOrDefault();
            var redirects = meta.Where(x => x.Name == DBNames.Redirect301 || x.Name==DBNames.Redirect302).ToList()
                .Select(x=>new KeyValuePair<string,string>(x.Name+x.Key,x.Value)).ToDictionary(x=>x.Key,x=>x.Value);
            var pathToLocale = meta.Where(x => x.Name == DBNames.PathToLocale).ToList().Select(x => new KeyValuePair<string, string>(x.Key, x.Value)).ToDictionary(x=>x.Key,x=>x.Value);
            var languages = meta.Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.Languages).ToList().Select(x=>x.Value).ToList();
            var fieldGroups = meta.Where(x => x.Name.StartsWith(DBNames.FieldGroups)).ToList();
            var typeAllowedTypes = meta.Where(x => x.Name == DBNames.TypeAllowedTypes).Select(x=>x.Key+":"+x.Value).ToList();
            var editorParameters = meta.Where(x => x.Name == DBNames.EditorSettings).Select(x=>x.Key).ToList();
            var cachePolicy = meta.Where(x => x.Name == DBNames.CachePolicy).Select(x=>x.Key+":"+x.Value).ToList();
            var typeAllowedTemplates = meta.Where(x => x.Name == DBNames.TypeAllowedTemplates).Select(x => x.Key + ":" + x.Value).ToList();
            model.TypeGroupField = new List<string>();
            
            fieldGroups.ForEach(x => {
                string typeName = x.Name.Replace(DBNames.FieldGroups,"");
                string groupName = x.Key;
                string FieldName = x.Value;
                model.TypeGroupField.Add(string.Concat(typeName,":",groupName,":",FieldName));                
            });
            model.TypeAllowedTemplates = typeAllowedTemplates;
            model.TypeAllowedTypes = typeAllowedTypes;
            model.DefaultLanguage = defaultLanguage == null ? "" : defaultLanguage.Value;
            model.EnableLocalePrefix = enableLocalePrefix == null ? false : bool.Parse(enableLocalePrefix.Value);
            model.Languages = languages;
            model.PathToLocale = pathToLocale;
            model.Redirect = redirects;
            model.EditorParameters = editorParameters;
            model.CachePolicy = cachePolicy;
            return View(model);
        }

        //
        // POST: /puck/Settings/Edit/5
        [HttpPost]
        public JsonResult Edit(Settings model)
        {
            string msg = "";
            bool success = false;
            try
            {
                // TODO: Add update logic here
                //default language
                if (!string.IsNullOrEmpty(model.DefaultLanguage)) {
                    var metaDL = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.DefaultLanguage).FirstOrDefault();
                    if (metaDL != null)
                    {
                        metaDL.Value = model.DefaultLanguage;
                    }
                    else {
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.Settings;
                        newMeta.Key = DBKeys.DefaultLanguage;
                        newMeta.Value = model.DefaultLanguage;
                        repo.AddPuckMeta(newMeta);
                    }
                }
                //enable local prefix
                var metaELP = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.EnableLocalePrefix).FirstOrDefault();
                if (metaELP != null)
                {
                    metaELP.Value = model.EnableLocalePrefix.ToString();
                }
                else {
                    var newMeta = new PuckMeta();
                    newMeta.Name = DBNames.Settings;
                    newMeta.Key = DBKeys.EnableLocalePrefix;
                    newMeta.Value = model.EnableLocalePrefix.ToString();
                    repo.AddPuckMeta(newMeta);
                }
                //languages
                if (model.Languages!=null && model.Languages.Count > 0)
                {
                    var metaLanguages = repo.GetPuckMeta().Where(x => x.Name == DBNames.Settings && x.Key == DBKeys.Languages).ToList();
                    if (metaLanguages.Count > 0)
                    {
                        metaLanguages.ForEach(x =>
                        {
                            repo.DeletePuckMeta(x);
                        });
                    }
                    model.Languages.ForEach(x => {
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.Settings;
                        newMeta.Key = DBKeys.Languages;
                        newMeta.Value = x;
                        repo.AddPuckMeta(newMeta);
                    });
                }
                //redirects
                if (model.Redirect!=null&&model.Redirect.Count > 0) {
                    var redirectMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.Redirect301 || x.Name==DBNames.Redirect302).ToList();
                    redirectMeta.ForEach(x => {
                        repo.DeletePuckMeta(x);
                    });
                    //count of 1 and key/value of null indicates delete only so inserts are skipped
                    if (!(model.Redirect.Count == 1 && string.IsNullOrEmpty(model.Redirect.First().Key)))
                    {
                        model.Redirect.ToList().ForEach(x =>
                        {
                            var newMeta = new PuckMeta();
                            newMeta.Name = x.Key.StartsWith(DBNames.Redirect301) ? DBNames.Redirect301 : DBNames.Redirect302;
                            newMeta.Key = x.Key.Substring(newMeta.Name.Length);
                            newMeta.Value = x.Value;
                            repo.AddPuckMeta(newMeta);
                        });
                    }
                }
                //fieldgroup
                if (model.TypeGroupField!=null&&model.TypeGroupField.Count > 0) {
                    foreach (var mod in apiHelper.AllModels(true))
                    {
                        var fieldGroupMeta = repo.GetPuckMeta().Where(x => x.Name.StartsWith(DBNames.FieldGroups+mod.AssemblyQualifiedName)).ToList();
                        fieldGroupMeta.ForEach(x =>
                        {
                            repo.DeletePuckMeta(x);
                        });
                    }
                    model.TypeGroupField.ForEach(x => {
                        var values = x.Split(new char[]{':'},StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.FieldGroups+values[0];
                        newMeta.Key = values[1];
                        newMeta.Value=values[2];
                        repo.AddPuckMeta(newMeta);
                    });
                }
                //typeallowedtypes
                if (model.TypeAllowedTypes != null && model.TypeAllowedTypes.Count > 0){
                    var typeAllowedTypesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTypes).ToList();
                    typeAllowedTypesMeta.ForEach(x => {
                        repo.DeletePuckMeta(x);
                    });
                    model.TypeAllowedTypes.ForEach(x => {
                        var values = x.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.TypeAllowedTypes;
                        newMeta.Key = values[0];
                        newMeta.Value = values[1];
                        repo.AddPuckMeta(newMeta);
                    });
                }
                //typeallowedtemplates
                if (model.TypeAllowedTemplates != null && model.TypeAllowedTemplates.Count > 0)
                {
                    var typeAllowedTemplatesMeta = repo.GetPuckMeta().Where(x => x.Name == DBNames.TypeAllowedTemplates).ToList();
                    typeAllowedTemplatesMeta.ForEach(x =>
                    {
                        repo.DeletePuckMeta(x);
                    });
                    model.TypeAllowedTemplates.ForEach(x =>
                    {
                        var values = x.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        var newMeta = new PuckMeta();
                        newMeta.Name = DBNames.TypeAllowedTemplates;
                        newMeta.Key = values[0];
                        newMeta.Value = values[1];
                        repo.AddPuckMeta(newMeta);
                    });
                }
                //cachepolicy
                if (model.CachePolicy == null)
                    model.CachePolicy = new List<string>();
                var cacheTypes = new List<string>();
                if (model.CachePolicy.Count > 0) {
                    foreach (var entry in model.CachePolicy) {
                        var type = entry.Split(new char[]{':'},StringSplitOptions.RemoveEmptyEntries)[0];
                        cacheTypes.Add(type);
                        var minutes = entry.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        int min;
                        if(!int.TryParse(minutes,out min))
                            throw new Exception("cache policy minutes not int for type:"+type);
                        var meta = repo.GetPuckMeta().Where(x => x.Name == DBNames.CachePolicy && x.Key.ToLower().Equals(type.ToLower())).FirstOrDefault();
                        if (meta != null)
                        {
                            meta.Value = minutes;
                        }
                        else {
                            meta = new PuckMeta() { Name=DBNames.CachePolicy,Key=type,Value=minutes};
                            repo.AddPuckMeta(meta);
                        }
                    }
                }
                //delete unset
                repo.GetPuckMeta().Where(x => x.Name == DBNames.CachePolicy && !cacheTypes.Contains(x.Key)).ToList().ForEach(x => repo.DeletePuckMeta(x));
                
                //orphan types
                if (model.Orphans != null && model.Orphans.Count > 0)
                {
                    foreach (var entry in model.Orphans) {
                        var t1 = entry.Key;
                        var t2 = entry.Value;
                        contentService.RenameOrphaned(t1, t2);
                    }
                }
                repo.SaveChanges();
                StateHelper.UpdateDefaultLanguage();
                StateHelper.UpdateCacheMappings();
                StateHelper.UpdateRedirectMappings();
                
                success = true;                
            }
            catch(Exception ex)
            {
                msg = ex.Message;
                success = false;
            }
            return Json(new { success=success,message = msg});
        }
                
    }
}
