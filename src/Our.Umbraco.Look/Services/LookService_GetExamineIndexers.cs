﻿using Examine;
using System.Linq;
using UmbracoExamine;

namespace Our.Umbraco.Look
{
    internal partial class LookService
    {
        /// <summary>
        /// Get all Examine indexers that Look should hook into
        /// </summary>
        /// <returns></returns>
        internal static BaseUmbracoIndexer[] GetExamineIndexers()
        {
            return ExamineManager
                        .Instance
                        .IndexProviderCollection
                        .Select(x => x as BaseUmbracoIndexer) // UmbracoContentIndexer, UmbracoMemberIndexer
                        .Where(x => x != null)
                        .Where(x => LookService.Instance.ExamineIndexers == null || LookService.Instance.ExamineIndexers.Contains(x.Name))
                        .ToArray();
        }
    }
}
