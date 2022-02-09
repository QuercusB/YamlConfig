using System;

namespace Quercus.YamlConfig
{
    public interface IConfigFile
    {
        IConfigStorage Config { get; }

        void Save();

        event EventHandler Saved;
    }
}
