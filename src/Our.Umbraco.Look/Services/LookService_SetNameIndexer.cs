﻿using Our.Umbraco.Look.Models;
using System;
using Umbraco.Core.Logging;

namespace Our.Umbraco.Look.Services
{
    public partial class LookService
    {
        /// <summary>
        /// Register consumer code to perform when indexing name
        /// </summary>
        /// <param name="nameIndexer">Your custom name indexing function</param>
        public static void SetNameIndexer(Func<IndexingContext, string> nameIndexer)
        {
            if (LookService.Instance.NameIndexer == null)
            {
                LogHelper.Info(typeof(LookService), "Name indexing function set");
            }
            else
            {
                LogHelper.Warn(typeof(LookService), "Name indexing function replaced");
            }

            LookService.Instance.NameIndexer = nameIndexer;
        }
    }
}
