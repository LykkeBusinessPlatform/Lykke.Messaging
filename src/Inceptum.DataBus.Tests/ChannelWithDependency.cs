﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Inceptum.DataBus;

namespace Inceptum.DataBus.Tests
{
    [Channel("ChannelWithDependency")]
    public class FeedWithDependency : IFeedProvider<int, string>
    {
        public IChannel<int> Channel1 { get; set; }

        public bool CanProvideFor(string context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(string c)
        {
            int i;
            return (int.TryParse(c, out i) ? Channel1.Feed(i) : null);
        }

        public IEnumerable<int> OnFeedLost(string context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(string context)
        {
            return null;
        }
    }


    [Channel("ChannelHavingFeedWithNotResolvableDependency")]
    public class FeedProviderWithNotResolvableDependency : IFeedProvider<int, string>
    {
        public FeedProviderWithNotResolvableDependency(ICloneable notResolvableDependency)
        {
        }

        public bool CanProvideFor(string context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(string c)
        {
            return Observable.Range(1,100);
        }

        public IEnumerable<int> OnFeedLost(string context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(string context)
        {
            return null;
        }
    }

    [Channel("Channel1")]
    public class FeedProvider1 : IFeedProvider<int, int>
    {
        public bool CanProvideFor(int context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(int c)
        {
            return Observable.Range(-c, 1);
        }

        public IEnumerable<int> OnFeedLost(int context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(int context)
        {
            return null;
        }
    }

    [Channel("Channel2")]
    public class FeedProvider2 : IFeedProvider<int, int>
    {
        public bool CanProvideFor(int context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(int c)
        {
            return Observable.Range(1, c);
        }

        public IEnumerable<int> OnFeedLost(int context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(int context)
        {
            return null;
        }
    }

    [Channel("Channel_With_Name")]
    public class ExplicitlyNamedChannelFeed : IFeedProvider<int, int>
    {
        public bool CanProvideFor(int context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(int c)
        {
            return Observable.Range(1, c);
        }

        public IEnumerable<int> OnFeedLost(int context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(int context)
        {
            return null;
        }
    }

    [Channel("FeedWithExplicitlyNamedDependencyChannel")]
    public class FeedWithExplicitlyNamedDependency : IFeedProvider<int, string>
    {
        [ImportChannel("Channel2")]
        public IChannel<int> Channel1 { get; set; }

        public bool CanProvideFor(string context)
        {
            return true;
        }

        public IObservable<int> CreateFeed(string c)
        {
            int i;
            return (int.TryParse(c, out i) ? Channel1.Feed(i) : null);
        }

        public IEnumerable<int> OnFeedLost(string context)
        {
            return new int[0];
        }

        public IFeedResubscriptionPolicy GetResubscriptionPolicy(string context)
        {
            return null;
        }
    }
}
