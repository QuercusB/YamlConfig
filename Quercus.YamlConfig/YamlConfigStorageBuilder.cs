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
    /// Build for YamlConfigStorage
    /// </summary>
    public class YamlConfigStorageBuilder
    {
        private INamingConvention namingConvention = NullNamingConvention.Instance;

        private List<IYamlTypeConverter> typeConverters = new List<IYamlTypeConverter>();

        public YamlConfigStorageBuilder WithNamingConvention(INamingConvention namingConvention)
        {
            this.namingConvention = namingConvention;
            return this;
        }

        public YamlConfigStorageBuilder WithTypeConverter(IYamlTypeConverter typeConverter)
        {
            typeConverters.Add(typeConverter);
            return this;
        }

        public YamlConfigStorageBuilder WithTypeConverter<T>() where T : IYamlTypeConverter, new()
        {
            typeConverters.Add(new T());
            return this;
        }

        public YamlConfigStorage Build()
        {
            var serializerBuilder = new SerializerBuilder()
                .WithNamingConvention(namingConvention)
                .IgnoreFields()
                .EnsureRoundtrip()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull);
            var deserializerBuilder = new DeserializerBuilder()
                .WithNamingConvention(namingConvention)
                .IgnoreFields()
                .IgnoreUnmatchedProperties();
            foreach (var typeConverter in typeConverters)
            {
                serializerBuilder = serializerBuilder.WithTypeConverter(typeConverter);
                deserializerBuilder = deserializerBuilder.WithTypeConverter(typeConverter);
            }
            return new YamlConfigStorage(serializerBuilder, deserializerBuilder);
        }
    }
}
