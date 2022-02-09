using System;
using System.IO;
using System.ComponentModel;
using System.Text;
using System.Runtime.CompilerServices;
using NLog;
using NUnit.Framework;
using FluentAssertions;
using YamlDotNet.Serialization;

namespace Quercus.YamlConfig
{
    [TestFixture]
    public class YamlConfigStorageTest
    {
        private readonly Logger Log = LogManager.GetCurrentClassLogger();

        private YamlConfigStorage Create()
        {
            return new YamlConfigStorage();
        }

        [Test]
        public void _01_Any_class_can_be_fetched_from_config_file()
        {
            var file = Create();

            file.Get<Sample>("sample").Should().NotBeNull();
            file.Get<Other>("other").Should().NotBeNull();
        }

        [Test]
        public void _02_If_some_class_is_fetched_by_some_key_then_attempt_to_fetch_another_type_should_fail()
        {
            var file = Create();

            file.Get<Sample>("sample").Should().NotBeNull("PRELIMINARY!!!");
            file.Invoking(f => file.Get<Other>("sample"))
                .Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void _03_Null_or_whitespace_block_name_are_not_allowed()
        {
            var file = Create();

            file.Invoking(_ => _.Get<Sample>(null))
                .Should().Throw<ArgumentNullException>();
            file.Invoking(_ => _.Get<Sample>(""))
                .Should().Throw<ArgumentNullException>();
            file.Invoking(_ => _.Get<Sample>("  "))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void _04_Configuration_block_can_be_set()
        {
            var file = Create();

            file.Invoking(_ => _.Set("sample", new Sample { X = 12 }))
                .Should().NotThrow();
        }

        [Test]
        public void _05_Configuration_block_can_not_be_set_with_null_or_whitespace_name()
        {
            var file = Create();

            file.Invoking(_ => _.Set(null, new Sample()))
                .Should().Throw<ArgumentNullException>();
            file.Invoking(_ => _.Set("", new Sample()))
                .Should().Throw<ArgumentNullException>();
            file.Invoking(_ => _.Set("  ", new Sample()))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void _06_Stored_configuration_block_can_be_fetched_and_it_should_be_same_object_as_was_stored()
        {
            var file = Create();

            var sample = new Sample { X = 12, Y = "Hello, world" };
            file.Set("sample", sample);

            var fetchSample = file.Get<Sample>("sample");
            fetchSample.Should().BeSameAs(sample);
        }

        [Test]
        public void _07_Storing_configuration_block_raises_a_PropertyChanged_event()
        {
            var file = Create();

            using (var monitor = file.Monitor())
            {
                file.Set("sample", new Sample { X = 1512 });
                monitor.Should().Raise(nameof(file.PropertyChanged)).WithArgs<PropertyChangedEventArgs>(
                    e => e.PropertyName == "sample");
            }
        }

        [Test]
        public void _08_Storing_configuration_block_raises_a_ContentChanged_event()
        {
            var file = Create();

            using (var monitor = file.Monitor())
            {
                file.Set("sample", new Sample { X = 1512 });
                monitor.Should().Raise(nameof(file.ContentChanged));
            }
        }

        [Test]
        public void _09_Empty_configuration_can_be_serialized()
        {
            var file = Create();

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                file.Save(writer);

            buffer.ToString().Should().Be(
                ToYaml(new { }));
        }

        [Test]
        public void _10_Configuration_with_several_blocks_can_be_serialized()
        {
            var file = Create();

            file.Set("Sample", new Sample { X = 12, Y = "Hello, world" });
            file.Set("Other", new Other { Y = "Another" });

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                file.Save(writer);

            buffer.ToString().Should().Be(
                ToYaml(new
                {
                    Sample = new { X = 12, Y = "Hello, world" },
                    Other = new { Y = "Another" }
                }));
        }

        [Test]
        public void _11_Empty_configuration_file_can_be_deserialized()
        {
            var file = Create();

            var yaml = ToYaml(new { });
            using (var reader = new StringReader(yaml))
                file.Invoking(_ => _.Load(reader)).Should().NotThrow();
        }

        [Test]
        public void _12_Configuration_file_with_blocks_can_be_deserialized()
        {
            var file = Create();

            var yaml = ToYaml(new
            {
                sample = new { X = 12, Y = "Hello, world" },
                other = new { Y = "Another" }
            });
            using (var reader = new StringReader(yaml))
                file.Invoking(_ => _.Load(reader)).Should().NotThrow();

            var sample = file.Get<Sample>("sample");
            sample.X.Should().Be(12);
            sample.Y.Should().Be("Hello, world");
            var other = file.Get<Other>("other");
            other.Y.Should().Be("Another");
        }

        [Test]
        public void _13_For_all_blocks_that_are_loaded_PropertyChanged_event_is_fired()
        {
            var file = Create();

            var yaml = ToYaml(new
            {
                sample = new { x = 12, y = "Hello, world" },
                other = new { y = "Another" }
            });
            using (var monitor = file.Monitor())
            {
                using (var reader = new StringReader(yaml))
                    file.Invoking(_ => _.Load(reader)).Should().NotThrow();

                monitor.Should().Raise(nameof(file.PropertyChanged))
                    .WithArgs<PropertyChangedEventArgs>(x => x.PropertyName == "sample");
                monitor.Should().Raise(nameof(file.PropertyChanged))
                    .WithArgs<PropertyChangedEventArgs>(x => x.PropertyName == "other");
            }
        }

        [Test]
        public void _14_If_block_was_present_in_config_but_doesnt_exist_in_yaml_event_for_it_is_fired_and_Get_returns_default()
        {
            var file = Create();
            file.Set("third", new Other { Y = "Third block" });

            var yaml = ToYaml(new
            {
                sample = new { x = 12, y = "Hello, world" },
                other = new { y = "Another" }
            });
            using (var monitor = file.Monitor())
            {
                using (var reader = new StringReader(yaml))
                    file.Invoking(_ => _.Load(reader)).Should().NotThrow();

                monitor.Should().Raise(nameof(file.PropertyChanged))
                    .WithArgs<PropertyChangedEventArgs>(x => x.PropertyName == "third");
                file.Get<Other>("third").Y.Should().Be(null);
            }
        }

        [Test]
        public void _15_If_Set_is_called_for_subclass_with_parent_class_extra_properties_are_not_saved()
        {
            var extended = new SampleExtended { X = 10, Y = "Hello", Extra = "World" };

            var file = Create();
            file.Set<Sample>("main", extended);

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                file.Save(writer);

            buffer.ToString().Should().Be(
                ToYaml(new
                {
                    main = new { X = 10, Y = "Hello" },
                }));
        }

        [Test]
        public void _16_Default_value_type_values_should_be_serialized()
        {
            var storage = Create();
            storage.Set("sample", new Sample());

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                storage.Save(writer);

            buffer.ToString().Should().Be(
                ToYaml(new
                {
                    sample = new
                    {
                        X = 0
                    }
                }));
        }

        [Test]
        public void _17_If_property_of_config_is_of_ILateBoundConfig_type_it_is_deserialized_as_YamlLateBoundConfig()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "Sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var block = storage.Get<BlockWithLateBound>("sample");
            block.Should().NotBeNull("PRELIMINARY");
            block.Kind.Should().Be("Sample", "PRELIMINARY");
            block.Config.Should().BeOfType<YamlLateBoundConfig>();
        }

        [Test]
        public void _18_From_YamlLateBoundConfig_we_can_later_cast_needed_type()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "Sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var block = storage.Get<BlockWithLateBound>("sample");
            block.Config.Should().BeOfType<YamlLateBoundConfig>("PRELIMINARY");
            var sample = block.Config.Cast<Sample>();
            sample.Should().NotBeNull();
            sample.X.Should().Be(10);
            sample.Y.Should().Be("Hello, world");

        }

        [Test]
        public void _19_If_configuration_block_is_not_present_but_has_ILateBoundConfig_property_it_is_initialized_with_YamlLateBoundConfig()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "Sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var block = storage.Get<BlockWithLateBound>("other");
            block.Config.Should().NotBeNull();
            block.Config.Should().BeOfType<YamlLateBoundConfig>();
        }

        [Test]
        public void _20_If_LateBoundConfig_is_changed_it_is_then_serialized()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "Sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var block = storage.Get<BlockWithLateBound>("other");
            block.Config.Should().NotBeNull("PRELIMINARY");

            block.Config.Set(new Sample { X = 512, Y = "A message" });

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                storage.Save(writer);

            buffer.ToString().Should().Be(ToYaml(new
            {
                sample = new { Kind = "Sample", Config = new { X = 10, Y = "Hello, world" } },
                other = new { Config = new { X = 512, Y = "A message" } }
            }));
        }

        [Test]
        public void _21_If_object_that_is_fetched_is_changed_then_when_serialized_it_is_serialized_with_updated_values()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { X = 10, Y = "Hello, world" }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var sample = storage.Get<Sample>("sample");
            sample.X = 5123;
            sample.Y = "Updated value";

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                storage.Save(writer);

            buffer.ToString().Should().Be(ToYaml(new
            {
                sample = new { X = 5123, Y = "Updated value" }
            }));
        }

        [Test]
        public void _22_If_object_fetched_implements_IPropertyChanged_interface_and_its_changed_ContentChanged_should_be_raised()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { X = 10, Y = "Hello, world" }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var sample = storage.Get<NotifiedSample>("sample");

            using (var monitor = storage.Monitor())
            {
                sample.X = 512;

                monitor.Should().Raise(nameof(storage.ContentChanged));
            }
        }

        [Test]
        public void _23_If_object_fetched_is_ILateBoundConfig_and_it_is_changed_then_when_serialized_it_should_be_updated_as_well()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var sample = storage.Get<BlockWithLateBound>("sample");
            var block = sample.Config.Cast<Sample>();

            block.X = 512;
            block.Y = "New text";

            var buffer = new StringBuilder();
            using (var writer = new StringWriter(buffer))
                storage.Save(writer);

            buffer.ToString().Should().Be(ToYaml(new
            {
                sample = new { Kind = "sample", Config = new { X = 512, Y = "New text" } }
            }));
        }

        [Test]
        public void _24_If_object_fetched_is_ILateBoundConfig_and_its_Config_is_changed_ContentChanged_should_be_raised()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var sample = storage.Get<BlockWithLateBound>("sample");

            using (var monitor = storage.Monitor())
            {
                sample.Config.Set(new Sample { X = 541, Y = "Some text" });

                monitor.Should().Raise(nameof(storage.ContentChanged));
            }
        }

        [Test]
        public void _25_If_object_fetched_is_ILateBoundConfig_and_its_Config_is_fetched_as_IPropertyChanged_and_it_is_changed_ContentChanged_should_be_raised()
        {
            var storage = Create();

            var yaml = ToYaml(new
            {
                sample = new { Kind = "sample", Config = new { X = 10, Y = "Hello, world" } }
            });
            using (var reader = new StringReader(yaml))
                storage.Load(reader);

            var sample = storage.Get<BlockWithLateBound>("sample");
            var block = sample.Config.Cast<NotifiedSample>();

            using (var monitor = storage.Monitor())
            {
                block.X = 512;

                monitor.Should().Raise(nameof(storage.ContentChanged));
            }
        }

        private static string ToYaml(dynamic @object)
        {
            return new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build().Serialize(@object);
        }

        public class Sample
        {
            public int X { get; set; }

            public string Y { get; set; }
        }

        public class NotifiedSample : INotifyPropertyChanged
        {
            private int x;
            private string y;

            public int X { get => x; set => Set(ref x, value); }

            public string Y { get => y; set => Set(ref y, value); }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (Equals(field, value))
                    return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class SampleExtended : Sample
        {
            public string Extra { get; set; }
        }

        public class Other
        {
            public string Y { get; set; }
        }

        public class BlockWithLateBound
        {
            public string Kind { get; set; }

            public ILateBoundConfig Config { get; set; }
        }
    }
}