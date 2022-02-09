using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Quercus.YamlConfig
{
    internal class YamlLateBoundConfigConverter : IYamlTypeConverter
    {
        private readonly YamlConfigStorage storage;

        internal YamlLateBoundConfigConverter(YamlConfigStorage storage)
        {
            this.storage = storage;
        }

        public bool Accepts(Type type)
        {
            return type == typeof(ILateBoundConfig) || type == typeof(YamlLateBoundConfig);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            return new YamlLateBoundConfig(storage, storage.SkipThisAndNestedEvents(parser));
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type)
        {
            if (!(value is YamlLateBoundConfig yamlLateBoundConfig))
            {
                emitter.Emit(new MappingStart());
                emitter.Emit(new MappingEnd());
                return;
            }
            if (yamlLateBoundConfig.EventList.Type == null)
            {
                if (yamlLateBoundConfig.EventList.Count > 0)
                {
                    foreach (var @event in yamlLateBoundConfig.EventList)
                        emitter.Emit(@event);
                }
                else
                {
                    emitter.Emit(new MappingStart());
                    emitter.Emit(new MappingEnd());
                }
            }
            else
            {
                if (yamlLateBoundConfig.EventList.Instance != null)
                    storage.Serializer.SerializeValue(emitter, yamlLateBoundConfig.EventList.Instance, yamlLateBoundConfig.EventList.Type);
                else
                {
                    emitter.Emit(new MappingStart());
                    emitter.Emit(new MappingEnd());
                }
            }
        }
    }
}