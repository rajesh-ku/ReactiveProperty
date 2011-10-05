﻿using System;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Xml.Linq;
using Codeplex.Reactive;
using Codeplex.Reactive.Notifier;
using Codeplex.Reactive.Asynchronous; // namespace for Asynchronous Extension Methods

namespace WPF.ViewModels
{
    // sample of asynchronous operation
    public class AsynchronousViewModel
    {
        public ReactiveProperty<string> SearchTerm { get; private set; }
        public ReactiveProperty<string> SearchingStatus { get; private set; }
        public ReactiveProperty<string> ProgressStatus { get; private set; }
        public ReactiveProperty<WikipediaModel[]> SearchResults { get; private set; }

        public AsynchronousViewModel()
        {
            // Notifier of network connecitng status/count
            var connect = new SignalNotifier();
            // Notifier of network progress report
            var progress = new ScheduledNotifier<DownloadProgressChangedEventArgs>();

            SearchTerm = new ReactiveProperty<string>();

            // Search asynchronous & result direct bind
            SearchResults = SearchTerm
                .Select(term =>
                {
                    connect.Increment(); // network open
                    return WikipediaModel.SearchTermAsync(term, progress)
                        .Finally(() => connect.Decrement()); // network close
                })
                .Switch() // flatten
                .ToReactiveProperty();

            // SignalChangedStatus : Increment(network open), Decrement(network close), Empty(all complete)
            SearchingStatus = connect
                .Select(x => (x != SignalChangedStatus.Empty) ? "loading..." : "complete")
                .ToReactiveProperty();

            ProgressStatus = progress
                .Select(x => string.Format("{0}/{1} {2}%", x.BytesReceived, x.TotalBytesToReceive, x.ProgressPercentage))
                .ToReactiveProperty();
        }
    }

    // Model - data & asynchronous request web api
    public class WikipediaModel
    {
        const string ApiFormat = "http://en.wikipedia.org/w/api.php?action=opensearch&search={0}&format=xml";

        public string Text { get; set; }
        public string Description { get; set; }

        public WikipediaModel(XElement item)
        {
            var ns = item.Name.Namespace;
            Text = (string)item.Element(ns + "Text");
            Description = (string)item.Element(ns + "Description");
        }

        // ***ObservableAsync Extensions Methods for WebClient
        // others, WebRequest/WebResponse/Stream Extensio Methods exists.
        public static IObservable<WikipediaModel[]> SearchTermAsync(string term, IProgress<DownloadProgressChangedEventArgs> progress)
        {
            var clinet = new WebClient();
            clinet.Headers[HttpRequestHeader.UserAgent] = "ReactiveProperty Sample";
            return clinet.DownloadStringObservableAsync(new Uri(string.Format(ApiFormat, term)), progress)
                .Select(Parse);
        }

        static WikipediaModel[] Parse(string rawXmlText)
        {
            var xml = XElement.Parse(rawXmlText);
            var ns = xml.Name.Namespace;
            return xml.Descendants(ns + "Item")
                .Select(x => new WikipediaModel(x))
                .ToArray();
        }

        public override string ToString()
        {
            return Text + ":" + Description;
        }
    }
}