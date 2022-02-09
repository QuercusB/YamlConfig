using System.IO;
using System.Linq.Expressions;
using System.Collections.Generic;
using FluentAssertions;
using NLog;
using NUnit.Framework;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace Quercus.YamlConfig
{
    [TestFixture]
    [Explicit("Research")]
    public class YamlResearchTest
    {
        private readonly Logger Log = LogManager.GetCurrentClassLogger();

        [Test]
        public void _01_We_can_deserialize_class_from_string()
        {
            var deserializer =
                new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
            var source = "hello:\r\n  world: 5";
            var result = deserializer.Deserialize<Sample>(source);

            result.Should().NotBeNull();
            result.Hello.Should().NotBeNull();
            result.Hello.World.Should().Be(5);
        }

        [Test]
        public void _02_We_can_split_source_string_into_named_blocks_and_later_parse_them_in_class()
        {
            var source = @"
main:
    x: 5
    y: 6
other:
    test: 346
third:
    test: 10
    text: Hello, world
";
            var blocks = new Dictionary<string, YamlCache>();
            using (var reader = new StringReader(source))
            {
                var parser = new YamlDotNet.Core.Parser(reader);
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                MappingStart mappingStart;
                if (parser.TryConsume(out mappingStart))
                {
                    while (parser.Current is Scalar s)
                    {
                        parser.MoveNext();
                        var blockName = s.Value;
                        Log.Debug($"Found block {blockName}");
                        blocks.Add(blockName, SkipThisAndNestedEvents(parser));
                    }
                    parser.Consume<MappingEnd>();
                }
                parser.Consume<DocumentEnd>();
                parser.Consume<StreamEnd>();
            }
            // blocks.Count.Should().Be(3);
            blocks.Should().ContainKey("main");
            blocks.Should().ContainKey("other");
            blocks.Should().ContainKey("third");
            Log.Debug("-- Events for main --------");
            foreach (var @event in blocks["main"])
                Log.Debug($"{@event.GetType()}: {@event.ToString()}");
            Log.Debug("---------------------------");
            Log.Debug("-- Events for other --------");
            foreach (var @event in blocks["other"])
                Log.Debug($"{@event.GetType()}: {@event.ToString()}");
            Log.Debug("---------------------------");
            Log.Debug("-- Events for third --------");
            foreach (var @event in blocks["third"])
                Log.Debug($"{@event.GetType()}: {@event.ToString()}");
            Log.Debug("---------------------------");
            var deserializer =
                new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
            var main = deserializer.Deserialize<Main>(blocks["main"].CreateParser());
            main.Should().NotBeNull();
            main.X.Should().Be(5);
            main.Y.Should().Be(6);
            var other = deserializer.Deserialize<Other>(blocks["other"].CreateParser());
            other.Should().NotBeNull();
            other.Test.Should().Be(346);
            other.Text.Should().Be("default");
            var third = deserializer.Deserialize<Other>(blocks["third"].CreateParser());
            third.Should().NotBeNull();
            third.Test.Should().Be(10);
            third.Text.Should().Be("Hello, world");
        }


        public YamlCache SkipThisAndNestedEvents(IParser parser)
        {
            var yamlCache = new YamlCache();
            var depth = 0;
            do
            {
                var next = parser.Consume<ParsingEvent>();
                depth += next.NestingIncrease;
                yamlCache.Add(next);
            }
            while (depth > 0);
            return yamlCache;
        }

        public class YamlCache : List<ParsingEvent>
        {
            public IParser CreateParser() => new YamlCacheParser(GetEnumerator());
        }

        public class YamlCacheParser : IParser
        {
            private readonly IEnumerator<ParsingEvent> enumerator;

            public YamlCacheParser(IEnumerator<ParsingEvent> enumerator)
            {
                this.enumerator = enumerator;
            }

            public ParsingEvent Current => enumerator.Current as ParsingEvent;

            public bool MoveNext() => enumerator.MoveNext();
        }

        private class Sample
        {
            public HelloClass Hello { get; set; }
        }

        private class HelloClass
        {
            public int World { get; set; }
        }

        private class Main
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        private class Other
        {
            public int Test { get; set; }
            public string Text { get; set; } = "default";
        }
    }
}