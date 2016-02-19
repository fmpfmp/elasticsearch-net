﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Elasticsearch.Net;
using FluentAssertions;
using Tests.Framework;

namespace Tests.ClientConcepts.ConnectionPooling.Sticky
{
    public class Sticky
    {
        /** Sticky
		 * Each connection pool returns the first `live` node so that it is sticky between requests
		*/

        protected int NumberOfNodes = 10;

        [U] public void EachViewStartsAtNextPositionAndWrapsOver()
        {
            var uris = Enumerable.Range(9200, NumberOfNodes).Select(p => new Uri("http://localhost:" + p));
            var staticPool = new StickyConnectionPool(uris);

            this.AssertCreateView(staticPool);
        }

        public void AssertCreateView(StickyConnectionPool pool)
        {
            /**
			* Here we have setup a static connection pool seeded with 10 nodes.
			* So what order we expect? Imagine the following:
			*
			* Thread A calls GetNext and gets returned the first live node
			* Thread B calls GetNext() and gets returned the same node as it's still the first live.
			*/

            var startingPositions = Enumerable.Range(0, NumberOfNodes)
                .Select(i => pool.CreateView().First())
                .Select(n => n.Uri.Port)
                .ToList();

            var expectedOrder = Enumerable.Repeat(9200, NumberOfNodes);
            startingPositions.Should().ContainInOrder(expectedOrder);
        }
    }
}
