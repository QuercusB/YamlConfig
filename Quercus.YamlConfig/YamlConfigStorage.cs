using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Quercus.YamlConfig
{
    /// <summary>
    /// Implementation of IConfig that loads and saves configuration into Yaml file.
    /// When serializing configuration block names of fields are converted to 
    /// camel-case (default behavior, can be overridden by constructor argument).
    /// 
    /// Deserialization ignores any extra-fields that may be present in Yaml.
    /// </summary>
    public class YamlConfigStorage : IConfigStorage
    {
        private IValueSerializer serializer;
        private IDeserializer deserializer;

        private Dictionary<string, ParsingEventList> blocks = new Dictionary<string, ParsingEventList>();

        public YamlConfigStorage(INamingConvention? namingConvention = null)
        {
            namingConvention = namingConvention ?? NullNamingConvention.Instance;
            var lateBoundConverter = new YamlLateBoundConfigConverter(this);
            serializer = new SerializerBuilder()
                .WithNamingConvention(namingConvention)
                .WithTypeConverter(lateBoundConverter)
                .IgnoreFields()
                .EnsureRoundtrip()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .BuildValueSerializer();
            deserializer = new DeserializerBuilder()
                .WithNamingConvention(namingConvention)
                .WithTypeConverter(lateBoundConverter)
                .IgnoreFields()
                .IgnoreUnmatchedProperties()
                .Build();
        }

        internal YamlConfigStorage(SerializerBuilder serializerBuilder, DeserializerBuilder deserializerBuilder)
        {
            var lateBoundConverter = new YamlLateBoundConfigConverter(this);
            serializer = serializerBuilder.WithTypeConverter(lateBoundConverter).BuildValueSerializer();
            deserializer = deserializerBuilder.WithTypeConverter(lateBoundConverter).Build();
        }

        internal IValueSerializer Serializer => serializer;
        internal IDeserializer Deserializer => deserializer;

        public object Get(Type configType, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            ParsingEventList eventList;
            if (!blocks.TryGetValue(name, out eventList))
            {
                eventList = new ParsingEventList { Type = configType, Instance = CreateDefault(configType) };
                eventList.ContentChanged += EventListContentChanged;
                blocks[name] = eventList;
            }
            else
            if (eventList.Type == null)
            {
                eventList.Type = configType;
                try
                {
                    eventList.Instance = deserializer.Deserialize(eventList.CreateParser(), configType) ?? CreateDefault(configType);
                }
                catch
                {
                    eventList.Instance = CreateDefault(configType);
                }
            }
            if (!configType.IsAssignableFrom(eventList.Type))
                throw new InvalidOperationException($"Tried to get config by key '{name}' as type '{configType}', while previously fetched it as type '{eventList.Type}'");
            return eventList.Instance;
        }

        internal object CreateDefault(Type configType)
        {
            var config = Activator.CreateInstance(configType);
            foreach (var property in configType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(ILateBoundConfig) && property.CanRead && property.CanWrite)
                {
                    var lateBoundEventList = new ParsingEventList();
                    lateBoundEventList.ContentChanged += EventListContentChanged;
                    property.SetValue(config, new YamlLateBoundConfig(this, lateBoundEventList));
                }
            }
            return config;
        }

        public void Set(Type configType, string name, object config)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));
            ParsingEventList eventList;
            if (!blocks.TryGetValue(name, out eventList))
            {
                eventList = new ParsingEventList { Type = configType, Instance = config };
                eventList.ContentChanged += EventListContentChanged;
                blocks[name] = eventList;
            }
            else
            {
                eventList.Type = configType;
                eventList.Instance = config;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Load(TextReader input)
        {
            var changedBlocks = new List<string>();
            var missingBlocks = new List<string>(blocks.Keys);
            var parser = new Parser(input);
            parser.Consume<StreamStart>();
            parser.Consume<DocumentStart>();
            MappingStart? mappingStart;
            if (parser.TryConsume<MappingStart>(out mappingStart))
            {
                while (parser.Current is Scalar s)
                {
                    parser.MoveNext();
                    var blockName = s.Value;
                    ParsingEventList existingBlock;
                    if (blocks.TryGetValue(blockName, out existingBlock))
                        existingBlock.ContentChanged -= EventListContentChanged;
                    blocks[blockName] = SkipThisAndNestedEvents(parser);
                    changedBlocks.Add(blockName);
                    missingBlocks.Remove(blockName);
                }
                parser.Consume<MappingEnd>();
            }
            parser.Consume<DocumentEnd>();
            parser.Consume<StreamEnd>();
            foreach (var blockName in changedBlocks)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(blockName));
            foreach (var missingBlock in missingBlocks)
            {
                blocks[missingBlock].ContentChanged -= EventListContentChanged;
                blocks.Remove(missingBlock);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(missingBlock));
            }
        }

        internal ParsingEventList SkipThisAndNestedEvents(IParser parser)
        {
            var eventList = new ParsingEventList();
            var depth = 0;
            do
            {
                var next = parser.Consume<ParsingEvent>();
                depth += next.NestingIncrease;
                eventList.Add(next);
            }
            while (depth > 0);
            eventList.ContentChanged += EventListContentChanged;
            return eventList;
        }

        public void Save(TextWriter output)
        {
            var emitter = new Emitter(output);
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart());
            emitter.Emit(new MappingStart());
            foreach (var block in blocks)
            {
                if (block.Value.Type == null)
                {
                    emitter.Emit(new Scalar(block.Key));
                    foreach (var @event in block.Value)
                        emitter.Emit(@event);
                }
                else
                if (block.Value.Instance != null)
                {
                    emitter.Emit(new Scalar(block.Key));
                    serializer.SerializeValue(emitter, block.Value.Instance, block.Value.Type);
                }
            }
            emitter.Emit(new MappingEnd());
            emitter.Emit(new DocumentEnd(true));
            emitter.Emit(new StreamEnd());
        }

        public event PropertyChangedEventHandler PropertyChanged = null!;

        private void EventListContentChanged(object sender, EventArgs e)
        {
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ContentChanged = null!;

        internal class ParsingEventList : List<ParsingEvent>, IEmitter
        {
            private object instance = null!;

            public void Emit(ParsingEvent @event) => Add(@event);

            public Type? Type { get; set; }

            public object Instance
            {
                get => instance;
                set
                {
                    if (Object.ReferenceEquals(instance, value))
                        return;
                    if (instance is INotifyPropertyChanged oldPropertyChanged)
                        oldPropertyChanged.PropertyChanged -= InstancePropertyChanged;
                    instance = value;
                    if (instance is INotifyPropertyChanged newPropertyChanged)
                        newPropertyChanged.PropertyChanged += InstancePropertyChanged;
                }
            }

            public IParser CreateParser() => new EnumeratingParser(GetEnumerator());

            private void InstancePropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                OnContentChanged();
            }

            public void OnContentChanged() =>
                ContentChanged?.Invoke(this, EventArgs.Empty);

            public event EventHandler ContentChanged = null!;
        }

        internal class EnumeratingParser : IParser
        {
            private readonly IEnumerator<ParsingEvent> enumerator;

            public EnumeratingParser(IEnumerator<ParsingEvent> enumerator)
            {
                this.enumerator = enumerator;
            }

            public ParsingEvent Current => enumerator.Current as ParsingEvent;

            public bool MoveNext() => enumerator.MoveNext();
        }
    }
}