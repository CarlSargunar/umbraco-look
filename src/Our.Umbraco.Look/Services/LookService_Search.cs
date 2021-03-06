﻿using Examine.LuceneEngine.Providers;
using Examine.LuceneEngine.SearchCriteria;
using Lucene.Net.Highlight;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Spatial.Tier;
using Lucene.Net.Util;
using Our.Umbraco.Look.Extensions;
using Our.Umbraco.Look.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace Our.Umbraco.Look.Services
{
    internal partial class LookService
    {
        /// <summary>
        /// Perform a Look search
        /// </summary>
        /// <param name="lookQuery">A LookQuery model for the search criteria</param>
        /// <returns>A LookResult model for the search response</returns>
        public static LookResult Search(LookQuery lookQuery)
        {
            // flag to indicate whether there are any query clauses in the supplied LookQuery
            bool hasQuery = lookQuery?.Compiled != null ? true : false;

            if (lookQuery == null)
            {
                return LookResult.Error("LookQuery object was null");
            }

            if (lookQuery.SearchingContext == null) // supplied by unit test to skip examine dependency
            {
                // attempt to get searching context from examine searcher name
                lookQuery.SearchingContext = LookService.GetSearchingContext(lookQuery.SearcherName);

                if (lookQuery.SearchingContext == null)
                {
                    return LookResult.Error("SearchingContext was null");
                }
            }

            if (lookQuery.Compiled == null)
            {
                BooleanQuery query = null; // the lucene query being built                                            
                Filter filter = null; // used for geospatial queries
                Sort sort = null;
                Func<string, IHtmlString> getHighlight = x => null;
                Func<int, double?> getDistance = x => null;

                query = new BooleanQuery();

                #region RawQuery

                if (!string.IsNullOrWhiteSpace(lookQuery.RawQuery))
                {
                    hasQuery = true;

                    query.Add(
                            new QueryParser(Lucene.Net.Util.Version.LUCENE_29, null, lookQuery.SearchingContext.Analyzer).Parse(lookQuery.RawQuery),
                            BooleanClause.Occur.MUST);
                }

                #endregion

                #region ExamineQuery

                if (lookQuery.ExamineQuery != null)
                {
                    var luceneSearchCriteria = lookQuery.ExamineQuery as LuceneSearchCriteria; // will be of type LookSearchCriteria when using the custom Look indexer/searcher

                    if (luceneSearchCriteria != null && luceneSearchCriteria.Query != null)
                    {
                        hasQuery = true;

                        query.Add(luceneSearchCriteria.Query, BooleanClause.Occur.MUST);
                    }
                }

                #endregion

                #region NodeQuery

                if (lookQuery.NodeQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasNodeField, "1")), BooleanClause.Occur.MUST);

                    // HasType
                    if (lookQuery.NodeQuery.Type != null)
                    {
                        query.Add(
                                new TermQuery(
                                    new Term(LookConstants.NodeTypeField, lookQuery.NodeQuery.Type.ToString())),
                                    BooleanClause.Occur.MUST);
                    }

                    // HasTypeAny
                    if (lookQuery.NodeQuery.TypeAny != null && lookQuery.NodeQuery.TypeAny.Any())
                    {
                        var nodeTypeQuery = new BooleanQuery();

                        foreach(var nodeType in lookQuery.NodeQuery.TypeAny)
                        {
                            nodeTypeQuery.Add(
                                new TermQuery(
                                    new Term(LookConstants.NodeTypeField, nodeType.ToString())),
                                    BooleanClause.Occur.SHOULD);
                        }

                        query.Add(nodeTypeQuery, BooleanClause.Occur.MUST);
                    }

                    // Detached
                    switch (lookQuery.NodeQuery.DetachedQuery)
                    {
                        case DetachedQuery.ExcludeDetached:

                            query.Add(
                                    new TermQuery(new Term(LookConstants.IsDetachedField, "1")),
                                    BooleanClause.Occur.MUST_NOT);

                            break;

                        case DetachedQuery.OnlyDetached:

                            query.Add(
                                new TermQuery(new Term(LookConstants.IsDetachedField, "1")),
                                BooleanClause.Occur.MUST);

                            break;
                    }

                    // HasCulture
                    if (lookQuery.NodeQuery.Culture != null)
                    {
                        query.Add(
                                new TermQuery(
                                    new Term(LookConstants.CultureField, lookQuery.NodeQuery.Culture.LCID.ToString())),
                                    BooleanClause.Occur.MUST);
                    }

                    // HasCultureAny
                    if (lookQuery.NodeQuery.CultureAny != null && lookQuery.NodeQuery.CultureAny.Any())
                    {
                        var nodeCultureQuery = new BooleanQuery();

                        foreach(var nodeCulture in lookQuery.NodeQuery.CultureAny)
                        {
                            nodeCultureQuery.Add(
                                new TermQuery(
                                    new Term(LookConstants.CultureField, nodeCulture.LCID.ToString())),
                                    BooleanClause.Occur.SHOULD);
                        }

                        query.Add(nodeCultureQuery, BooleanClause.Occur.MUST);
                    }

                    // HasAlias
                    if (lookQuery.NodeQuery.Alias != null)
                    {
                        query.Add(
                                new TermQuery(
                                    new Term(LookConstants.NodeAliasField, lookQuery.NodeQuery.Alias.ToString())),
                                    BooleanClause.Occur.MUST);
                    }

                    // HasAliasAny
                    if (lookQuery.NodeQuery.AliasAny != null && lookQuery.NodeQuery.AliasAny.Any())
                    {
                        var nodeAliasQuery = new BooleanQuery();

                        foreach (var typeAlias in lookQuery.NodeQuery.AliasAny)
                        {
                            nodeAliasQuery.Add(
                                            new TermQuery(
                                                new Term(LookConstants.NodeAliasField, typeAlias)),
                                                BooleanClause.Occur.SHOULD);
                        }

                        query.Add(nodeAliasQuery, BooleanClause.Occur.MUST);
                    }

                    // Ids
                    if (lookQuery.NodeQuery.Ids != null && lookQuery.NodeQuery.Ids.Any())
                    {
                        if (lookQuery.NodeQuery.NotIds != null)
                        {
                            var conflictIds = lookQuery.NodeQuery.Ids.Where(x => lookQuery.NodeQuery.NotIds.Contains(x));

                            if (conflictIds.Any())
                            {
                                return LookResult.Error($"Conflict in NodeQuery, Ids: '{ string.Join(",", conflictIds) }' are in both Ids and NotIds");
                            }
                        }

                        var idQuery = new BooleanQuery();

                        foreach (var id in lookQuery.NodeQuery.Ids)
                        {
                            idQuery.Add(
                                        new TermQuery(new Term(LookConstants.NodeIdField, id.ToString())),
                                        BooleanClause.Occur.SHOULD);
                        }

                        query.Add(idQuery, BooleanClause.Occur.MUST);
                    }

                    // Keys
                    if (lookQuery.NodeQuery.Keys != null && lookQuery.NodeQuery.Keys.Any())
                    {
                        if (lookQuery.NodeQuery.NotKeys != null)
                        {
                            var conflictKeys = lookQuery.NodeQuery.Keys.Where(x => lookQuery.NodeQuery.NotKeys.Contains(x));

                            if (conflictKeys.Any())
                            {
                                return LookResult.Error($"Conflict in NodeQuery, keys: '{ string.Join(",", conflictKeys) }' are in both Keys and NotKeys");
                            }
                        }

                        var keyQuery = new BooleanQuery();

                        foreach (var key in lookQuery.NodeQuery.Keys)
                        {
                            keyQuery.Add(
                                        new TermQuery(new Term(LookConstants.NodeKeyField, key.GuidToLuceneString())),
                                        BooleanClause.Occur.SHOULD);
                        }

                        query.Add(keyQuery, BooleanClause.Occur.MUST);
                    }

                    // NotId
                    if (lookQuery.NodeQuery.NotId != null)
                    {
                        query.Add(
                                new TermQuery(new Term(LookConstants.NodeIdField, lookQuery.NodeQuery.NotId.ToString())),
                                BooleanClause.Occur.MUST_NOT);
                    }

                    // NotIds
                    if (lookQuery.NodeQuery.NotIds != null && lookQuery.NodeQuery.NotIds.Any())
                    {
                        foreach (var exculudeId in lookQuery.NodeQuery.NotIds)
                        {
                            query.Add(
                                    new TermQuery(new Term(LookConstants.NodeIdField, exculudeId.ToString())),
                                    BooleanClause.Occur.MUST_NOT);
                        }
                    }

                    // NotKey
                    if (lookQuery.NodeQuery.NotKey != null)
                    {
                        query.Add(
                                new TermQuery(new Term(LookConstants.NodeKeyField, lookQuery.NodeQuery.NotKey.ToString())),
                                BooleanClause.Occur.MUST_NOT);
                    }

                    // NotKeys
                    if (lookQuery.NodeQuery.NotKeys != null && lookQuery.NodeQuery.NotKeys.Any())
                    {
                        foreach (var excludeKey in lookQuery.NodeQuery.NotKeys)
                        {
                            query.Add(
                                    new TermQuery(new Term(LookConstants.NodeKeyField, excludeKey.GuidToLuceneString())),
                                    BooleanClause.Occur.MUST_NOT);
                        }
                    }
                }

                #endregion

                #region NameQuery

                if (lookQuery.NameQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasNameField, "1")), BooleanClause.Occur.MUST);

                    string wildcard1 = null;
                    string wildcard2 = null; // incase Contains specified with StartsWith and/or EndsWith

                    if (!string.IsNullOrEmpty(lookQuery.NameQuery.StartsWith))
                    {
                        if (!string.IsNullOrEmpty(lookQuery.NameQuery.Is))
                        {
                            if (!lookQuery.NameQuery.Is.StartsWith(lookQuery.NameQuery.StartsWith))
                            {
                                return LookResult.Error("Conflict in NameQuery between Is and StartsWith");
                            }
                        }
                        else
                        {
                            wildcard1 = lookQuery.NameQuery.StartsWith + "*";
                        }
                    }

                    if (!string.IsNullOrEmpty(lookQuery.NameQuery.EndsWith))
                    {
                        if (!string.IsNullOrEmpty(lookQuery.NameQuery.Is))                            
                        {
                            if (!lookQuery.NameQuery.Is.EndsWith(lookQuery.NameQuery.EndsWith))
                            {
                                return LookResult.Error("Conflict in NameQuery between Is and EndsWith");
                            }                            
                        }
                        else
                        {
                            if (wildcard1 == null)
                            {
                                wildcard1 = "*" + lookQuery.NameQuery.EndsWith;
                            }
                            else
                            {
                                wildcard1 += lookQuery.NameQuery.EndsWith;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(lookQuery.NameQuery.Contains))
                    {
                        if (!string.IsNullOrEmpty(lookQuery.NameQuery.Is))
                        {
                            if (!lookQuery.NameQuery.Is.Contains(lookQuery.NameQuery.Contains))
                            {
                                return LookResult.Error("Conflict in NameQuery between Is and Contains");
                            }
                        }
                        else
                        {
                            if (wildcard1 == null)
                            {
                                wildcard1 = "*" + lookQuery.NameQuery.Contains + "*";
                            }
                            else
                            {
                                wildcard2 = "*" + lookQuery.NameQuery.Contains + "*";
                            }
                        }
                    }

                    var nameField = lookQuery.NameQuery.CaseSensitive ? LookConstants.NameField : LookConstants.NameField + "_Lowered";

                    if (wildcard1 != null)
                    {
                        var wildcard = lookQuery.NameQuery.CaseSensitive ? wildcard1 : wildcard1.ToLower();

                        query.Add(new WildcardQuery(new Term(nameField, wildcard)), BooleanClause.Occur.MUST);

                        if (wildcard2 != null)
                        {
                            wildcard = lookQuery.NameQuery.CaseSensitive ? wildcard2 : wildcard2.ToLower();

                            query.Add(new WildcardQuery(new Term(nameField, wildcard)), BooleanClause.Occur.MUST);
                        }
                    }

                    if (!string.IsNullOrEmpty(lookQuery.NameQuery.Is))
                    {
                        var isText = lookQuery.NameQuery.CaseSensitive ? lookQuery.NameQuery.Is : lookQuery.NameQuery.Is.ToLower();

                        query.Add(new TermQuery(new Term(nameField, isText)), BooleanClause.Occur.MUST);
                    }
                }

                #endregion

                #region DateQuery

                if (lookQuery.DateQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasDateField, "1")), BooleanClause.Occur.MUST);

                    if (lookQuery.DateQuery.After.HasValue || lookQuery.DateQuery.Before.HasValue)
                    {
                        var includeLower = lookQuery.DateQuery.After == null || lookQuery.DateQuery.Boundary == DateBoundary.Inclusive || lookQuery.DateQuery.Boundary == DateBoundary.BeforeExclusiveAfterInclusive;
                        var includeUpper = lookQuery.DateQuery.Before == null || lookQuery.DateQuery.Boundary == DateBoundary.Inclusive || lookQuery.DateQuery.Boundary == DateBoundary.BeforeInclusiveAfterExclusive;

                        query.Add(
                                new TermRangeQuery(
                                        LookConstants.DateField,
                                        lookQuery.DateQuery.After.DateToLuceneString() ?? DateTime.MinValue.DateToLuceneString(),
                                        lookQuery.DateQuery.Before.DateToLuceneString() ?? DateTime.MaxValue.DateToLuceneString(),
                                        includeLower,
                                        includeUpper),
                                BooleanClause.Occur.MUST);
                    }
                }

                #endregion

                #region TextQuery

                if (lookQuery.TextQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasTextField, "1")), BooleanClause.Occur.MUST);

                    if (!string.IsNullOrWhiteSpace(lookQuery.TextQuery.SearchText))
                    {
                        var queryParser = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, LookConstants.TextField, lookQuery.SearchingContext.Analyzer);

                        Query searchTextQuery = null;

                        try
                        {
                            searchTextQuery = queryParser.Parse(lookQuery.TextQuery.SearchText);
                        }
                        catch
                        {
                            return LookResult.Error($"Unable to parse LookQuery.TextQuery.SearchText: '{ lookQuery.TextQuery.SearchText }' into a Lucene query");
                        }

                        if (searchTextQuery != null)
                        {
                            query.Add(searchTextQuery, BooleanClause.Occur.MUST);

                            if (lookQuery.TextQuery.GetHighlight)
                            {
                                var queryScorer = new QueryScorer(searchTextQuery.Rewrite(lookQuery.SearchingContext.IndexSearcher.GetIndexReader()));

                                var highlighter = new Highlighter(new SimpleHTMLFormatter("<strong>", "</strong>"), queryScorer);

                                getHighlight = (x) =>
                                {
                                    var tokenStream = lookQuery.SearchingContext.Analyzer.TokenStream(LookConstants.TextField, new StringReader(x));

                                    var highlight = highlighter.GetBestFragments(
                                                                    tokenStream,
                                                                    x,
                                                                    1, // max number of fragments
                                                                    "...");

                                    return new HtmlString(highlight);
                                };
                            }
                        }
                    }
                }

                #endregion

                #region TagQuery

                if (lookQuery.TagQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasTagsField, "1")), BooleanClause.Occur.MUST);

                    // Has
                    if (lookQuery.TagQuery.Has != null)
                    {
                        query.Add(
                                new TermQuery(new Term(LookConstants.TagsField + lookQuery.TagQuery.Has.Group, lookQuery.TagQuery.Has.Name)),
                                BooleanClause.Occur.MUST);
                    }

                    // Not
                    if (lookQuery.TagQuery.Not != null)
                    {
                        query.Add(
                                new TermQuery(new Term(LookConstants.TagsField + lookQuery.TagQuery.Not.Group, lookQuery.TagQuery.Not.Name)),
                                BooleanClause.Occur.MUST_NOT);
                    }

                    // HasAll
                    if (lookQuery.TagQuery.HasAll != null && lookQuery.TagQuery.HasAll.Any())
                    {
                        foreach (var tag in lookQuery.TagQuery.HasAll)
                        {
                            query.Add(
                                    new TermQuery(new Term(LookConstants.TagsField + tag.Group, tag.Name)),
                                    BooleanClause.Occur.MUST);
                        }
                    }

                    // HasAllOr
                    if (lookQuery.TagQuery.HasAllOr != null && lookQuery.TagQuery.HasAllOr.Any() && lookQuery.TagQuery.HasAllOr.SelectMany(x => x).Any())
                    {
                        var orQuery = new BooleanQuery();

                        foreach (var tagCollection in lookQuery.TagQuery.HasAllOr)
                        {
                            if (tagCollection.Any())
                            {
                                var allTagQuery = new BooleanQuery();

                                foreach (var tag in tagCollection)
                                {
                                    allTagQuery.Add(
                                        new TermQuery(new Term(LookConstants.TagsField + tag.Group, tag.Name)),
                                        BooleanClause.Occur.MUST);
                                }

                                orQuery.Add(allTagQuery, BooleanClause.Occur.SHOULD);
                            }
                        }

                        query.Add(orQuery, BooleanClause.Occur.MUST);
                    }

                    // HasAny
                    if (lookQuery.TagQuery.HasAny != null && lookQuery.TagQuery.HasAny.Any())
                    {
                        var anyTagQuery = new BooleanQuery();

                        foreach (var tag in lookQuery.TagQuery.HasAny)
                        {
                            anyTagQuery.Add(
                                            new TermQuery(new Term(LookConstants.TagsField + tag.Group, tag.Name)),
                                            BooleanClause.Occur.SHOULD);
                        }

                        query.Add(anyTagQuery, BooleanClause.Occur.MUST);
                    }

                    // HasAnyAnd
                    if (lookQuery.TagQuery.HasAnyAnd != null && lookQuery.TagQuery.HasAnyAnd.Any())
                    {
                        foreach (var tagCollection in lookQuery.TagQuery.HasAnyAnd)
                        {
                            if (tagCollection.Any())
                            {
                                var anyTagQuery = new BooleanQuery();

                                foreach (var tag in tagCollection)
                                {
                                    anyTagQuery.Add(
                                                    new TermQuery(new Term(LookConstants.TagsField + tag.Group, tag.Name)),
                                                    BooleanClause.Occur.SHOULD);
                                }

                                query.Add(anyTagQuery, BooleanClause.Occur.MUST);
                            }
                        }
                    }

                    // NotAny
                    if (lookQuery.TagQuery.NotAny != null && lookQuery.TagQuery.NotAny.Any())
                    {
                        foreach (var tag in lookQuery.TagQuery.NotAny)
                        {
                            query.Add(
                                new TermQuery(new Term(LookConstants.TagsField + tag.Group, tag.Name)),
                                BooleanClause.Occur.MUST_NOT);
                        }
                    }
                }

                #endregion

                #region LocationQuery
                
                if (lookQuery.LocationQuery != null)
                {
                    hasQuery = true;

                    query.Add(new TermQuery(new Term(LookConstants.HasLocationField, "1")), BooleanClause.Occur.MUST);

                    if (lookQuery.LocationQuery.Boundary != null) // limit results within an lat lng fixed view (eg, typical map bounds)
                    {
                        query.Add(
                                new TermRangeQuery(
                                    LookConstants.LocationField + "_Latitude",
                                    NumericUtils.DoubleToPrefixCoded(lookQuery.LocationQuery.Boundary.LatitudeMin),
                                    NumericUtils.DoubleToPrefixCoded(lookQuery.LocationQuery.Boundary.LatitudeMax),
                                    true,
                                    true),
                                BooleanClause.Occur.MUST);
                        
                        query.Add(
                                new TermRangeQuery(
                                    LookConstants.LocationField + "_Longitude",
                                    NumericUtils.DoubleToPrefixCoded(lookQuery.LocationQuery.Boundary.LongitudeMin),
                                    NumericUtils.DoubleToPrefixCoded(lookQuery.LocationQuery.Boundary.LongitudeMax),
                                    true,
                                    true),
                                BooleanClause.Occur.MUST);
                    }

                    if (lookQuery.LocationQuery.Location != null) // location set, so can calculate distance
                    {
                        double maxDistance = LookService._maxDistance;

                        if (lookQuery.LocationQuery.MaxDistance != null)
                        {
                            maxDistance = Math.Min(lookQuery.LocationQuery.MaxDistance.GetMiles(), maxDistance);
                        }

                        var distanceQueryBuilder = new DistanceQueryBuilder(
                                                    lookQuery.LocationQuery.Location.Latitude,
                                                    lookQuery.LocationQuery.Location.Longitude,
                                                    maxDistance,
                                                    LookConstants.LocationField + "_Latitude",
                                                    LookConstants.LocationField + "_Longitude",
                                                    LookConstants.LocationTierFieldPrefix,
                                                    true);

                        filter = distanceQueryBuilder.Filter;

                        if (lookQuery.SortOn == SortOn.Distance)
                        {
                            sort = new Sort(
                                        new SortField(
                                            LookConstants.DistanceField,
                                            new DistanceFieldComparatorSource(distanceQueryBuilder.DistanceFilter)));
                        }

                        getDistance = new Func<int, double?>(x =>
                        {
                            if (distanceQueryBuilder.DistanceFilter.Distances.ContainsKey(x))
                            {
                                return distanceQueryBuilder.DistanceFilter.Distances[x];
                            }

                            return null;
                        });
                    }
                }

                #endregion

                if (hasQuery)
                {
                    switch (lookQuery.SortOn)
                    {
                        case SortOn.Name: // a -> z
                            sort = new Sort(new SortField(LuceneIndexer.SortedFieldNamePrefix + LookConstants.NameField, SortField.STRING));
                            break;

                        case SortOn.DateAscending: // oldest -> newest
                            sort = new Sort(new SortField(LuceneIndexer.SortedFieldNamePrefix + LookConstants.DateField, SortField.LONG, false));
                            break;

                        case SortOn.DateDescending: // newest -> oldest
                            sort = new Sort(new SortField(LuceneIndexer.SortedFieldNamePrefix + LookConstants.DateField, SortField.LONG, true));
                            break;

                        // SortOn.Distance already set (if valid)
                    }

                    lookQuery.Compiled = new LookQueryCompiled(
                                                        lookQuery,
                                                        query,
                                                        filter,
                                                        sort ?? new Sort(SortField.FIELD_SCORE),
                                                        getHighlight,
                                                        getDistance);
                }
            }

            if (!hasQuery)
            {
                return LookResult.Error("No query clauses supplied"); // empty failure
            }

            TopDocs topDocs = lookQuery
                                .SearchingContext
                                .IndexSearcher
                                .Search(
                                    lookQuery.Compiled.Query,
                                    lookQuery.Compiled.Filter,
                                    LookService._maxLuceneResults,
                                    lookQuery.Compiled.Sort);

            if (topDocs.TotalHits > 0)
            {
                List<Facet> facets = null;

                if (lookQuery.TagQuery != null && lookQuery.TagQuery.FacetOn != null)
                {
                    facets = new List<Facet>();

                    Query facetQuery = lookQuery.Compiled.Filter != null 
                                            ? (Query)new FilteredQuery(lookQuery.Compiled.Query, lookQuery.Compiled.Filter) 
                                            : lookQuery.Compiled.Query;

                    // do a facet query for each group in the array
                    foreach (var group in lookQuery.TagQuery.FacetOn.TagGroups)
                    {
                        var simpleFacetedSearch = new SimpleFacetedSearch(
                                                        lookQuery.SearchingContext.IndexSearcher.GetIndexReader(),
                                                        LookConstants.TagsField + group);

                        var facetResult = simpleFacetedSearch.Search(facetQuery);

                        facets.AddRange(
                                facetResult
                                    .HitsPerFacet
                                    .Select(
                                        x => new Facet()
                                        {
                                            Tags = new LookTag[] { new LookTag(group, x.Name.ToString()) },
                                            Count = Convert.ToInt32(x.HitCount)
                                        }
                                    ));
                    }
                }

                return new LookResult(
                                lookQuery,
                                topDocs,
                                facets != null ? facets.ToArray() : new Facet[] { });
            }

            return LookResult.Empty(); // empty success
        }
    }
}
