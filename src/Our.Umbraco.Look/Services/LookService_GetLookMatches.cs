﻿using Examine.LuceneEngine.Providers;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Our.Umbraco.Look.Extensions;
using Our.Umbraco.Look.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Umbraco.Core.Models;

namespace Our.Umbraco.Look.Services
{
    public partial class LookService
    {
        /// <summary>
        /// Supplied with the result of a Lucene query, this method will yield a constructed LookMatch for each in order
        /// </summary>
        /// <param name="indexSearcher">The searcher supplied to get the Lucene doc for each id in the Lucene results (topDocs)</param>
        /// <param name="topDocs">The results of the Lucene query (a collection of ids in an order)</param>
        /// <param name="requestFields">Enum value specifying which Lucene fields shoudl be returned</param>
        /// <param name="getHighlight">Function used to get the highlight text for a given result text</param>
        /// <param name="getDistance">Function used to calculate distance (if a location was supplied in the original query)</param>
        /// <returns></returns>
        private static IEnumerable<LookMatch> GetLookMatches(
                                                    IndexSearcher indexSearcher,
                                                    TopDocs topDocs,
                                                    RequestFields requestFields,
                                                    Func<string, IHtmlString> getHighlight,
                                                    Func<int, double?> getDistance)
        {
            MapFieldSelector mapFieldSelector = null; // when null, all fields are returned

            // these fields are always requested
            var lookFieldNames = new string[] {
                LuceneIndexer.IndexNodeIdFieldName,  // "__NodeId"
                LookConstants.NodeTypeField,
                LookConstants.NameField,
                LookConstants.DateField,
                LookConstants.TextField,
                LookConstants.AllTagsField,
                LookConstants.LocationField
            };

            if (requestFields == RequestFields.LookFields) 
            {
                // limit fields to be returned
                mapFieldSelector = new MapFieldSelector(lookFieldNames);
            }

            // there should always be a valid node type value to parse
            var getNodeType = new Func<string, PublishedItemType>(x => { Enum.TryParse(x, out PublishedItemType type); return type; });
            
            // helper to simplify call below
            var getTags = new Func<Field[], LookTag[]>(x => {
                if (x != null) { return x.Select(y => new LookTag(y.StringValue())).ToArray(); }
                return new LookTag[] { };
            });

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                var docId = scoreDoc.doc;

                var doc = indexSearcher.Doc(docId, mapFieldSelector);

                string[] fieldNames;

                if (requestFields == RequestFields.AllFields)
                {
                    fieldNames = doc
                                    .GetFields()
                                    .Cast<Field>()
                                    .Select(x => x.Name())
                                    .Union(lookFieldNames)
                                    .Distinct()
                                    .ToArray();
                }
                else
                {
                    fieldNames = lookFieldNames;
                }

                var fieldSingleValues = new Dictionary<string, string>();
                var fieldMultiValues = new Dictionary<string, string[]>();

                foreach (var fieldName in fieldNames)
                {
                    var values = doc.GetValues(fieldName);

                    if (!(values.Length > 1))
                    {
                        fieldSingleValues.Add(fieldName, values.FirstOrDefault());
                    }
                    else
                    {
                        fieldMultiValues.Add(fieldName, values);
                    }
                }

                var lookMatch = new LookMatch(
                    scoreDoc.doc,
                    scoreDoc.score,
                    Convert.ToInt32(fieldSingleValues[LuceneIndexer.IndexNodeIdFieldName]),
                    fieldSingleValues[LookConstants.NameField],
                    fieldSingleValues[LookConstants.DateField].LuceneStringToDate(),
                    fieldSingleValues[LookConstants.TextField],
                    getHighlight(fieldSingleValues[LookConstants.TextField]),
                    getTags(doc.GetFields(LookConstants.AllTagsField)),
                    fieldSingleValues[LookConstants.LocationField] != null ? Location.FromString(fieldSingleValues[LookConstants.LocationField]) : null,
                    getDistance(docId),
                    fieldSingleValues,
                    fieldMultiValues,
                    getNodeType(fieldSingleValues[LookConstants.NodeTypeField]),
                    LookService.Instance.UmbracoHelper
                );
                
                yield return lookMatch;
            }
        }
    }
}
