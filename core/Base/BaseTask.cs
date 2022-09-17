﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using puck.core.Abstract;
using puck.core.Attributes;
using Newtonsoft.Json;
using puck.core.Events;
using puck.core.Constants;
using puck.core.State;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace puck.core.Base
{
    public class BaseTask
    {
        public BaseTask() {
            this.RunOn = DateTime.Now;
            this.IntervalSeconds = 60;
        }

        protected IServiceProvider ServiceProvider { get { return PuckCache.ServiceProvider; } }

        [HiddenInput(DisplayValue=false)]
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Recurring { get; set; }
        public int IntervalSeconds { get; set; }
        public DateTime RunOn { get; set; }
        [HiddenInput(DisplayValue = false)]
        public DateTime? LastRun { get; set; }

        private object lck = new object();
        int lock_wait = 100;
        public event EventHandler<DispatchEventArgs> TaskEnd;
        public void DoRun(CancellationToken t) {
            bool taken = false;
            try
            {
                Monitor.TryEnter(lck, lock_wait, ref taken);
                if (!taken)
                    return;
                var task = Run(t);
                task.GetAwaiter().GetResult();
            }
            catch (Exception ex) {
                PuckCache.PuckLog.Log(ex);
            }
            finally
            {
                try {
                    this.LastRun = DateTime.Now;
                    if (TaskEnd != null)
                        TaskEnd(this, new DispatchEventArgs { Task = this });
                } catch (Exception ex) {
                    PuckCache.PuckLog.Log(ex);
                }
                if (taken)
                    Monitor.Exit(lck);
            }            
        }
        public virtual async Task Run(CancellationToken t){

        }        
    }
}
