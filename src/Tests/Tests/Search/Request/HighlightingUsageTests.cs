﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using Elastic.Xunit.XunitPlumbing;
using Nest;
using Tests.Framework.Integration;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Tests.Core.Extensions;
using Tests.Core.ManagedElasticsearch.Clusters;
using Tests.Domain;
using Xunit;
using Tests.Framework;
using Tests.Framework.ManagedElasticsearch.Clusters;

namespace Tests.Search.Request
{
	/**
	* Allows to highlight search results on one or more fields.
	* The implementation uses either the lucene `highlighter`, `fast-vector-highlighter` or `postings-highlighter`.
	*
	* See the Elasticsearch documentation on {ref_current}/search-request-highlighting.html[highlighting] for more detail.
	*/
	public class HighlightingUsageTests : SearchUsageTestBase
	{
		public HighlightingUsageTests(ReadOnlyCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		public string LastNameSearch { get; } = Project.Instance.LeadDeveloper.LastName;

		protected override object ExpectJson => new
		{
			query = new
			{
				match = new Dictionary<string, object>
				{
					{ "name.standard", new Dictionary<string, object>
						{
							{ "query", "Upton Sons Shield Rice Rowe Roberts" }
						}
					}
				}
			},
			highlight = new
			{
				pre_tags = new[] { "<tag1>" },
				post_tags = new[] { "</tag1>" },
				encoder = "html",
				fields = new Dictionary<string, object>
				{
					{ "name.standard", new Dictionary<string, object>
						{
							{ "type", "plain" },
							{ "force_source", true},
							{ "fragment_size", 150 },
							{ "fragmenter", "span" },
							{ "number_of_fragments", 3},
							{ "no_match_size", 150 }
						}
					},
					{ "leadDeveloper.firstName", new Dictionary<string, object>
						{
							{ "type", "fvh" },
							{ "phrase_limit", 10 },
							{ "boundary_max_scan", 50 },
							{ "pre_tags", new [] { "<name>" } },
							{ "post_tags", new [] { "</name>" } },
							{ "highlight_query", new Dictionary<string, object>
								{
									{ "match", new Dictionary<string, object>
										{
											{ "leadDeveloper.firstName", new Dictionary<string, object>
												{
													{ "query", "Kurt Edgardo Naomi Dariana Justice Felton" }
												}
											}
										}
									}
								}
							}
						}
					},
					{ "state.offsets", new Dictionary<string, object>
						{
							{ "type", "postings" },
							{ "pre_tags", new [] { "<state>" } },
							{ "post_tags", new [] { "</state>" } },
							{ "highlight_query", new Dictionary<string, object>
								{
									{ "terms", new Dictionary<string, object>
										{
											{ "state.offsets", new [] { "stable" , "bellyup" } }
										}
									}
								}
							}
						}
					}
				}
			}
		};

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Query(q => q
				.Match(m => m
					.Field(f => f.Name.Suffix("standard"))
					.Query("Upton Sons Shield Rice Rowe Roberts")
				)
			)
			.Highlight(h => h
				.PreTags("<tag1>")
				.PostTags("</tag1>")
				.Encoder("html")
				.Fields(
					fs => fs
						.Field(p => p.Name.Suffix("standard"))
						.Type("plain")
						.ForceSource()
						.FragmentSize(150)
						.Fragmenter(HighlighterFragmenter.Span)
						.NumberOfFragments(3)
						.NoMatchSize(150),
					fs => fs
						.Field(p => p.LeadDeveloper.FirstName)
						.Type(HighlighterType.Fvh)
						.PreTags("<name>")
						.PostTags("</name>")
						.BoundaryMaxScan(50)
						.PhraseLimit(10)
						.HighlightQuery(q => q
							.Match(m => m
								.Field(p => p.LeadDeveloper.FirstName)
								.Query("Kurt Edgardo Naomi Dariana Justice Felton")
							)
						),
					fs => fs
						.Field(p => p.State.Suffix("offsets"))
						.Type(HighlighterType.Postings)
						.PreTags("<state>")
						.PostTags("</state>")
						.HighlightQuery(q => q
							.Terms(t => t
								.Field(f => f.State.Suffix("offsets"))
								.Terms(
									StateOfBeing.Stable.ToString().ToLowerInvariant(),
									StateOfBeing.BellyUp.ToString().ToLowerInvariant()
								)
							)
						)
				)
			);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Query = new MatchQuery
				{
					Query = "Upton Sons Shield Rice Rowe Roberts",
					Field = "name.standard"
				},
				Highlight = new Highlight
				{
					PreTags = new[] { "<tag1>" },
					PostTags = new[] { "</tag1>" },
					Encoder = "html",
					Fields = new Dictionary<Field, IHighlightField>
					{
						{ "name.standard", new HighlightField
							{
								Type = HighlighterType.Plain,
								ForceSource = true,
								FragmentSize = 150,
								Fragmenter = HighlighterFragmenter.Span,
								NumberOfFragments = 3,
								NoMatchSize = 150
							}
						},
						{ "leadDeveloper.firstName", new HighlightField
							{
								Type = "fvh",
								PhraseLimit = 10,
								BoundaryMaxScan = 50,
								PreTags = new[] { "<name>"},
								PostTags = new[] { "</name>"},
								HighlightQuery = new MatchQuery
								{
									Field = "leadDeveloper.firstName",
									Query = "Kurt Edgardo Naomi Dariana Justice Felton"
								}
							}
						},
						{ "state.offsets", new HighlightField
							{
								Type = HighlighterType.Postings,
								PreTags = new[] { "<state>"},
								PostTags = new[] { "</state>"},
								HighlightQuery = new TermsQuery
								{
									Field = "state.offsets",
									Terms = new [] { "stable", "bellyup" }
								}
							}
						}
					}
				}
			};

		protected override void ExpectResponse(ISearchResponse<Project> response)
		{
			response.ShouldBeValid();

			foreach (var highlightsInEachHit in response.Hits.Select(d=>d.Highlights))
			{
				foreach (var highlightField in highlightsInEachHit)
				{
					if (highlightField.Key == "name.standard")
					{
						foreach (var highlight in highlightField.Value.Highlights)
						{
							highlight.Should().Contain("<tag1>");
							highlight.Should().Contain("</tag1>");
						}
					}
					else if (highlightField.Key == "leadDeveloper.firstName")
					{
						foreach (var highlight in highlightField.Value.Highlights)
						{
							highlight.Should().Contain("<name>");
							highlight.Should().Contain("</name>");
						}
					}
					else if (highlightField.Key == "state.offsets")
					{
						foreach (var highlight in highlightField.Value.Highlights)
						{
							highlight.Should().Contain("<state>");
							highlight.Should().Contain("</state>");
						}
					}
					else
					{
						Assert.True(false, $"highlights contains unexpected key {highlightField.Key}");
					}
				}
			}
		}
	}

	/**
	 * [float]
	 * == Unified highlighter
	 *
	 * The unified highlighter can extract offsets from either postings, term vectors, or via re-analyzing text.
	 * Under the hood it uses Lucene UnifiedHighlighter which picks its strategy depending on the
	 * field and the query to highlight.
	 *
	 * [NOTE]
	 * --
	 * Unified highlighter is available only in Elasticsearch 5.3.0+
	 * --
	 *
	 * [WARNING]
	 * --
	 * This functionality is experimental and may be changed or removed completely in a future release.
	 * Elastic will take a best effort approach to fix any issues, but experimental features
	 * are not subject to the support SLA of official GA features.
	 * --
	 */
	[SkipVersion("<5.3.0", "unified highlighter introduced in 5.3.0")]
	public class UnifiedHighlightingUsageTests : SearchUsageTestBase
	{
		public UnifiedHighlightingUsageTests(ReadOnlyCluster cluster, EndpointUsage usage) : base(cluster, usage) { }

		public string LastNameSearch { get; } = Project.First.LeadDeveloper.LastName;

		protected override object ExpectJson => new
		{
			query = new
			{
				match = new JObject
				{
					{ "name.standard", new JObject
						{
							{ "query", "Upton Sons Shield Rice Rowe Roberts" }
						}
					}
				}
			},
			highlight = new
			{
				fields = new JObject
				{
					{ "leadDeveloper.lastName", new JObject
						{
							{ "type", "unified" },
							{ "pre_tags", new JArray { "<name>" } },
							{ "post_tags", new JArray { "</name>" } },
							{ "highlight_query", new JObject
								{
									{ "match", new JObject
										{
											{ "leadDeveloper.lastName", new JObject
												{
													{ "query", LastNameSearch }
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		};

		protected override Func<SearchDescriptor<Project>, ISearchRequest> Fluent => s => s
			.Query(q => q
				.Match(m => m
					.Field(f => f.Name.Suffix("standard"))
					.Query("Upton Sons Shield Rice Rowe Roberts")
				)
			)
			.Highlight(h => h
				.Fields(
					fs => fs
						.Field(p => p.LeadDeveloper.LastName)
						.Type(HighlighterType.Unified)
						.PreTags("<name>")
						.PostTags("</name>")
						.HighlightQuery(q => q
							.Match(m => m
								.Field(p => p.LeadDeveloper.LastName)
								.Query(LastNameSearch)
							)
						)
				)
			);

		protected override SearchRequest<Project> Initializer =>
			new SearchRequest<Project>
			{
				Query = new MatchQuery
				{
					Query = "Upton Sons Shield Rice Rowe Roberts",
					Field = "name.standard"
				},
				Highlight = new Highlight
				{
					Fields = new Dictionary<Field, IHighlightField>
					{
						{ "leadDeveloper.lastName", new HighlightField
							{
								Type = HighlighterType.Unified,
								PreTags = new[] { "<name>"},
								PostTags = new[] { "</name>"},
								HighlightQuery = new MatchQuery
								{
									Field = "leadDeveloper.lastName",
									Query = LastNameSearch
								}
							}
						}
					}
				}
			};

		protected override void ExpectResponse(ISearchResponse<Project> response)
		{
			response.ShouldBeValid();

			foreach (var highlightsInEachHit in response.Hits.Select(d => d.Highlights))
			{
				foreach (var highlightField in highlightsInEachHit)
				{
					if (highlightField.Key == "leadDeveloper.lastName")
					{
						foreach (var highlight in highlightField.Value.Highlights)
						{
							highlight.Should().Contain("<name>");
							highlight.Should().Contain("</name>");
						}
					}
					else
					{
						Assert.True(false, $"highlights contains unexpected key {highlightField.Key}");
					}
				}
			}
		}
	}
}