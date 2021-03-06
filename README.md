# Look (Beta) for Umbraco v7
Look sits on top of Umbraco Examine adding support for:

* Indexing all IPublishedContent items (each as a Lucene Document) be they: Umbraco Content, Media, Members or properties on them that return IPublishedContent, eg. Nested Content. 

* Text match highlighting - return fragments of contextual text relevant to the supplied search term.

* Geospatial querying - boundary and location distance querying (this can also be used for filtering / sorting).

* Tag querying & faceting - query on tags and return facet data for tags.

## Installation

There are two NuGet packages:

[Our.Umbraco.Look](https://www.nuget.org/packages/Our.Umbraco.Look) installs a single assembly _Our.Umbraco.Look.dll_ with dependencies on: 

  * Umbraco 7.3.0 (min)
  * Examine 0.1.70 (min)
  * Lucene.Net.Contrib 2.9.4.1 (min)

[Our.Umbraco.Look.BackOffice](https://www.nuget.org/packages/Our.Umbraco.Look.BackOffice) installs an assembly _Our.Umbraco.Look.BackOffice.dll_ and files in App_Plugins/Look with a dependency on:

  * Our.Umbraco.Look

 ## Indexing

Look can be used index additional `name`, `date`, `text`, `tag` and `location` fields item into existing Examine Lucene indexes (no config file changes required) and/or a Look Examine indexer
can be configured which will also index 'detached' IPublsihedContent.

To implement indexing behaviour, functions can be set via static methods on the LookConfiguration class (all are optional).

```csharp
public static class LookConfiguration
{
	// specify which Examine indexers to hook into (if not set, then all will be used by default)
	public static string[] ExamineIndexers { get; set; }

	// creates case sensitive and case insensitive fields (not analyzed) - for use with NameQuery
	public static Func<IndexingContext, string> NameIndexer { set; }

	// creates a date & sorting fields - for use with DateQuery
	public static Func<IndexingContext, DateTime?> DateIndexer { set; }

	// creates a text field (analyzed) - for use with TextQuery
	public static Func<IndexingContext, string> TextIndexer { set; }

	// creates a tag field for each tag group - for use with TagQuery
	public static Func<IndexingContext, LookTag[]> TagIndexer { set; }

	// creates multple fields - for use with LocationQuery
	public static Func<IndexingContext, Location> LocationIndexer { set; }
}
```

The model supplied to the indexing functions:

```csharp
public class IndexingContext
{
	/// <summary>
	/// The name of the Examine indexer into which this item is being indexed
	/// </summary>
	public string IndexerName { get; }

	/// <summary>
	/// The Content, Media, Member or Detached item being indexed (always has a value)
	/// </summary>
	public IPublishedContent Item { get; }

	/// <summary>
	/// When a detached item is being indexed, this property will be the hosting content, media or member 
	/// (this value will null when the item being indexed is not Detached)
	/// </summary>
	public IPublishedContent HostItem { get; }
}
```
[Example Indexing Code](../../wiki/Example-Indexing)


## Searching

Searching is performed using an (Umbraco or Look) Examine Searcher and can be done using the Exmaine API, or with the Look API.

### Look API

The Look API can be used with all searchers. Eg.

```csharp
var lookQuery = new LookQuery(); // use the default Examine searcher (usually "ExternalSearcher")
```

or

```csharp
var lookQuery = new LookQuery("MyLookSearcher"); // use a named Examine searcher
```

```csharp
lookQuery.NodeQuery = ...
lookQuery.NameQuery = ...
lookQuery.DateQuery = ...
lookQuery.TextQuery = ...
lookQuery.TagQuery = ...
lookQuery.LocationQuery = ...
lookQuery.ExamineQuery = ...
lookQuery.RawQuery = ...

var results = lookQuery.Search();
```

### Look Query types

All query types are optional, but when set, they become a required clause. 


#### NodeQuery
A node query is used to specify search criteria based on common properties of IPublishedContent (all properties are optional).

```csharp
lookQuery.NodeQuery = new NodeQuery() {
	Type = PublishedItemType.Content,
	TypeAny = new [] { 
		PublishedItemType.Content, 
		PublishedItemType.Media, 
		PublishedItemType.Member 
	},
	DetachedQuery = DetachedQuery.IncludeDetached, // enum options
	Culture = new CultureInfo("fr"),
	CultureAny = new [] {
		new CultureInfo("fr")	
	},
	Alias = "myDocTypeAlias",
	AliasAny = new [] { 
		"myDocTypeAlias", 
		"myMediaTypeAlias",
		"myMemberTypeAlias"
	},
	Ids = new [] { 1, 2 },
	Keys = new [] { 
		Guid.Parse("dc890492-4571-4701-8085-b874837d597a"), 
		Guid.Parse("9f60f10f-74ea-4323-98bb-13b6f6423ad6"),
	}
	NotId = 3, // (eg. exclude current page)
	NotIds = new [] { 4, 5 },
	NotKey = Guid.Parse("3e919e10-b702-4478-87ed-4a42ec52b337"),
	NotKeys = new [] { 
		Guid.Parse("6bb24ed2-9466-422f-a9d4-27a805db2d47"), 
		Guid.Parse("88a9e4e3-d4cb-4641-aff3-8579f1d60399")
	}
};
```

#### NameQuery
A name query is used together with a custom name indexer and enables string comparrison queries (wildcards are not allowed and all properties are optional).
If a name query is set (ie, not null), then results must have a name value.

```csharp
lookQuery.NameQuery = new NameQuery() {
	Is = "Abc123Xyz",
	StartsWith = "Abc",
	Contains = "123",
	EndsWith = "Xyz",
	CaseSensitive = true // applies to all: Is, StartsWith, Contains & EndsWith
};
```

#### DateQuery

A date query is used together with a custom date indexer and enables date range queries (all properties are optional).
If a date query is set (ie, not null), then results must have a date value.

```csharp
lookQuery.DateQuery = new DateQuery() {
	After = new DateTime(2005, 02, 16),
	Before = null,
	Boundary = DateBoundary.Inclusive
}
```

#### TextQuery

A text query is used together with a custom text indexer and allows for wildcard searching using the analyzer specified by Exmaine.
Highlighting gives the ability to return an html snippet of text indiciating the part of the full text that the match was made on. All properties
are optional).

```csharp
lookQuery.TextQuery = new TextQuery() {
	SearchText = "some text to search for",
	GetHighlight = true // return highlight extract from the text field containing the search text
}
```

#### TagQuery

A tag query is used together with a custom tag indexer (all properties are optional).
If a tag query is set then only results with tags are returned.

```csharp
lookQuery.TagQuery = new TagQuery() {    

	// must have this tag
	Has = new LookTag("color:red"),

	// must not have this tag
	Not = new LookTag("colour:white"),

	// must have all these tags
	HasAll = TagQuery.MakeTags("colour:red", "colour:blue"),

	// must have all tags from at least one of these collections
	HasAllOr = new LookTag[][] {
		TagQuery.MakeTags("colour:red", "size:large"),
		TagQuery.MakeTags("colour:red", "size:small")
	}

	// must have at least one of these tags
	HasAny = TagQuery.MakeTags("color:green", "colour:yellow"),

	// must have at least one tag from each collection
	HasAnyAnd = new LookTag[][] { 
		TagQuery.MakeTags("colour:red", "size:large"), 
		TagQuery.MakeTags("colour:red", "size:medium")
	},

	// must not have any of these tags
	NotAny = TagQuery.MakeTags("colour:black"),

	// return facet data for the tag groups
	FacetOn = new TagFacetQuery("colour", "size", "shape")
};
```

##### LookTags

A tag can be any string and exists within an optionally specified group (if a group isn't set, then the tag is put into a default un-named group - String.Empty).
A group string must only contain aphanumberic/underscore chars, and be less than 50 chars (as it is also used to generate a custom Lucene field name).

A LookTag can be constructed from specified group and tag values:

```csharp
LookTag(string group, string name)
```

or from a raw string value:

```csharp
LokTag(string value)
````

When constructing from a raw string value, the first colon char ':' is used as an optional delimiter between a group and tag.
eg.

```csharp
var tag1 = new LookTag("red"); // tag 'red', in default un-named group
var tag2 = new LookTag(":red"); // tag 'red', in default un-named group
var tag2 = new LookTag("colour:red"); // tag 'red', in group 'colour'
```

There is also a static helper on the TagQuery model which can be used as a shorthand to create a LookTag array. Eg.

```csharp
var tags = TagQuery.MakeTags("colour:red", "colour:green", "colour:blue", "size:large");
```


#### LocationQuery

A location query is used together with a custom location indexer. 
All properties are optional, but if a LocationQuery is set, then only results with a location will be returned.
A Boundary can be set using two points to define a pane on the latitude/longitude axis.
If a Location is set, then a distance value returned. However if a MaxDistance is also set, then only results
within that range are returned.

```csharp
lookQuery.LocationQuery = new LocationQuery() {
	Boundary = new Boundary(
		new Location(55, 10),
		new Location(56, 11)
	),
	Location = new Location(55.406330, 10.388500),
	MaxDistance = new Distance(500, DistanceUnit.Miles)
};
```

#### ExamineQuery

Examine ISearchCriteria can be passed into a LookQuery.

```charp
lookQuery.ExamineQuery = myExamineQuery.Compile();
```

#### RawQuery

String property for any [Lucene raw query](http://www.lucenetutorial.com/lucene-query-syntax.html).

```csharp
lookQuery.RawQuery = "+myField: myValue";
```

#### SortOn

If not specified then the reults will be sorted on the Lucene score, otherwise sorting can be set by the SortOn enum to use the custom name, date or distance fields.

### Search Results

The search can be performed by calling the Search method on the LookQuery object:

```csharp
var lookResult = lookQuery.Search();
```

When the search is performed, the source LookQuery model is compiled (such that it can be useful to hold onto a reference for any subsequent paging queries). 

The LookResult model returned implements Examine.ISearchResults, but extends it with a Matches property that will return the results enumerated as strongly typed LookMatch objects (useful for lazy access to the assocated IPublishedContent item and other data) and a Facets property for any facet results.

```csharp
public class LookResult : ISearchResults
{
	/// <summary>
	/// When true, indicates the Look Query was parsed and executed correctly
	/// </summary>
	public bool Success { get; }
	
	/// <summary>
	/// Expected total number of results expected in the result enumerable
	/// </summary>
	public int TotalItemCount { get; }

	/// <summary>
	/// Get the results enumerable as LookMatch objects
	/// </summary>
	public IEnumerable<LookMatch> Matches { get; }

	/// <summary>
	/// Any returned facets
	/// </summary>
	public Facet[] Facets { get; }
}
```
