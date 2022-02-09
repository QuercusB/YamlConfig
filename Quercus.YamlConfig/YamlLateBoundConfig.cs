using System.ComponentModel;
using System;
using YamlDotNet.Core.Events;

namespace Quercus.YamlConfig
{
    /// <summary>
    /// Property of this type can be used to cast config block to certain type after reading parent block
    /// </summary>
    public class YamlLateBoundConfig : ILateBoundConfig
    {
        private readonly YamlConfigStorage storage;
        private YamlConfigStorage.ParsingEventList eventList;

        internal YamlLateBoundConfig(YamlConfigStorage storage, YamlConfigStorage.ParsingEventList eventList)
        {
            this.storage = storage;
            this.eventList = eventList;
        }

        internal YamlConfigStorage.ParsingEventList EventList => eventList;

        public object Cast(Type type)
        {
            if (eventList.Type == null)
            {
                eventList.Type = type;
                try
                {
                    eventList.Instance = storage.Deserializer.Deserialize(eventList.CreateParser(), type) ??
                        storage.CreateDefault(type);
                }
                catch
                {
                    eventList.Instance = storage.CreateDefault(type);
                }
            }
            if (!type.IsAssignableFrom(eventList.Type))
                throw new InvalidOperationException($"Tried to cast ILateBoundConfig to type '{type}' while previously casted it as '{eventList.Type}'");
            return eventList.Instance;
        }

        public void Set(Type type, object config)
        {
            eventList.Type = type;
            eventList.Instance = config;
            if (type == null)
                eventList.Clear();
            eventList.OnContentChanged();
        }
    }
}
