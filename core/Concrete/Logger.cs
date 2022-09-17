﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using puck.core.Abstract;
using Microsoft.Extensions.Hosting;
using puck.core.Helpers;

namespace puck.core.Concrete
{
    public class Logger:I_Log
    {
        public string DATADIRECTORY { get; set; }
        private static Object log_lock = new Object();
        public Logger() { 
            DATADIRECTORY= ApiHelper.MapPath($"~/App_Data/Log/{ApiHelper.ServerName()}");
        }
        public Logger(string logPath) {
            DATADIRECTORY = logPath;
        }
        public void Log(Exception ex) {
            if (ex == null) return;
            var message = ex.Message ?? "";
            var tempEx = ex;
            var maxExceptions = 10;
            var count = 0;
            while (tempEx.InnerException != null && count < maxExceptions)
            {
                if (!string.IsNullOrEmpty(tempEx.InnerException.Message))
                    message += $" - inner exception: {tempEx.InnerException.Message}";
                tempEx = tempEx.InnerException;
                count++;
            }
            this.Log(message,ex.StackTrace,"error",ex.GetType());
        }
        public void Log(string message,string stackTrace,string level="error",Type exceptionType=null)
        {
            lock (log_lock)
            {
                var dname = DATADIRECTORY;
                var fname = DateTime.Now.ToString("yyyy-MM-dd");
                var ext = ".txt";
                var maxlen = 5000000;
                
                if (!Directory.Exists(dname))
                {
                    Directory.CreateDirectory(dname);
                }
                var di = new DirectoryInfo(dname);
                //var fc = di.GetFiles().Length;
                //var lfpath = dname + "\\" + fname + (fc == 0 ? 0 : fc - 1).ToString() + ext;
                var lfpath = dname + "\\" + fname + ext;
                StreamWriter sw = null;
                if (!File.Exists(lfpath))
                {
                    sw = File.CreateText(lfpath);
                }
                //var fi = new FileInfo(lfpath);
                //if (fi.Length > maxlen)
                //{
                //    lfpath = dname + "\\" + fname + fc.ToString() + ext;
                //    sw = File.CreateText(lfpath);
                //}
                if (sw == null) sw = File.AppendText(lfpath);
                sw.WriteLine("|" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                sw.WriteLine("|" + level);
                sw.WriteLine("|"+ exceptionType?.Name??"");
                sw.WriteLine("|" + message);
                sw.WriteLine("|" + stackTrace);
                sw.WriteLine("\n");
                sw.Close();
            }
        }
    }
}