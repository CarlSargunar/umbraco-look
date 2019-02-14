﻿using Our.Umbraco.Look.BackOffice.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Our.Umbraco.Look.BackOffice.Services
{
    /// <summary>
    /// common queries used by both the tree and api
    /// </summary>
    internal static class QueryService
    {
        /// <summary>
        /// get all tag groups in seacher
        /// </summary>
        /// <param name="searcherName"></param>
        /// <returns></returns>
        internal static string[] GetTagGroups(string searcherName) //TODO: change to Dictionary<string, int> (to assoicate a count)
        {
            // TODO: return useage count for each (need new field)

            return new LookQuery(searcherName) { TagQuery = new TagQuery() }
                        .Search()
                        .Matches
                        .SelectMany(x => x.Tags.Select(y => y.Group))
                        .Distinct()
                        .OrderBy(x => x)
                        .ToArray();
        }

        /// <summary>
        /// get all tags in group and gives each a count
        /// </summary>
        /// <param name="searcherName"></param>
        /// <param name="tagGroup"></param>
        /// <returns></returns>
        internal static Dictionary<LookTag, int> GetTags(string searcherName, string tagGroup) //TODO: change to Dictionary<LookTag, int> (to assoicate a count)
        {
            return new LookQuery(searcherName) { TagQuery = new TagQuery() { FacetOn = new TagFacetQuery(tagGroup) } }
                                .Search()
                                .Facets
                                .Select(x => new Tuple<LookTag, int>(x.Tags.Single(), x.Count))
                                .OrderBy(x => x.Item1.Name)
                                .ToDictionary(x => x.Item1, x => x.Item2);
        }

        /// <summary>
        /// get a chunk of matches
        /// </summary>
        /// <param name="tagName"></param>
        /// <param name="sort"></param>
        /// <returns></returns>
        internal static MatchesResult GetMatches(string searcherName, string sort, int skip, int take)
        {
            var matchesResult = new MatchesResult();

            var lookQuery = new LookQuery(searcherName);

            lookQuery.NodeQuery = new NodeQuery();

            lookQuery.SortOn = SortOn.Name;

            var lookResult = lookQuery.Search();

            matchesResult.TotalItemCount = lookResult.TotalItemCount;
            matchesResult.Matches = lookResult
                                        .Matches
                                        .Skip(skip)
                                        .Take(take)
                                        .Select(x => (MatchesResult.Match)x)
                                        .ToArray();

            return matchesResult;
        }


        /// <summary>
        /// get a chunk of matches
        /// </summary>
        /// <param name="searcherName"></param>
        /// <param name="tagGroup"></param>
        /// <param name="tagName"></param>
        /// <param name="sort"></param>
        /// <returns></returns>
        internal static MatchesResult GetTagMatches(string searcherName, string tagGroup, string tagName, string sort, int skip, int take)
        {
            var matchesResult = new MatchesResult();

            var lookQuery = new LookQuery(searcherName);
            var tagQuery = new TagQuery(); // setting a tag query, means only items that have tags will be returned

            if (string.IsNullOrWhiteSpace(tagName)) // only have the group to query
            {
                // TODO: add a new field into the lucene index (would avoid additional query to first look up the tags in this group)
                var tagsInGroup = QueryService.GetTags(searcherName, tagGroup).Select(x => x.Key).ToArray();

                tagQuery.Any = new LookTag[][] { tagsInGroup };

            }
            else // we have a specifc tag
            {
                tagQuery.All = new[] { new LookTag(tagGroup, tagName) };
            }


            lookQuery.TagQuery = tagQuery;
            lookQuery.SortOn = SortOn.Name;

            var lookResult = lookQuery.Search();

            matchesResult.TotalItemCount = lookResult.TotalItemCount;
            matchesResult.Matches = lookResult
                                        .Matches
                                        .Skip(skip)
                                        .Take(take)
                                        .Select(x => (MatchesResult.Match)x)
                                        .ToArray();

            return matchesResult;
        }
    }
}
