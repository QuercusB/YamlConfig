using System;
using System.IO;
using System.ComponentModel;

namespace Quercus.YamlConfig
{
    /// <summary>
    /// Interface for configurations storage.
    /// It allows to store and fetch any class as named configuration block.
    /// Surely, it should better be plain .NET class with set of properties, for which getter and setter is defined.
    /// Otherwise, correctness of serialization will depend on actual IConfig implementation.
    /// 
    /// Idea is to allow any system component to request and store any its settings without any intervention
    /// in other code, that code of component itself
    /// 
    /// To allow different consumers to use (and change) same configuration block independently - 
    /// method Get always return different instance of requested class (actually deserializing class instance on each call)
    /// This allows, for example, configuration editor to change the object and be sure it doesn't influence
    /// other parts of system until Set method is invoked.
    /// When Set is invoked all consumers will receive event that certain block was changed.
    /// 
    /// Expected consumers can be of 3 types:
    /// - Consumer of conifguration block
    ///   This class takes instance of IConfigStorage, using Get method fetches named configuration block
    ///   and uses it.
    ///   It may also subscribe to PropertyChanged event to get notified if corresponding configuration block was
    ///   changed (PropertyName in PropertyChangedEventArgs correspond to name of configuration block)
    /// - Editor of configuration
    ///   Again it fetches named configuration block
    ///   After making change to it, it calls Set method and configuration block is stored in IConfigStorage implementation,
    ///   PropertyChangedEvent is raised informing other consumers of change if certain block
    /// - Application
    ///   On application start calls Load, passing certain TextReader and IConfigStorage should read the reader
    ///   and create all blocks. For each loaded block PropertyChangedEvent is fired
    ///   When needed (requested by user, timeout, application exit) application calls Save and stores full
    ///   configuration file to given writer
    /// </summary>
    public interface IConfigStorage : INotifyPropertyChanged
    {
        object Get(Type configType, string name);

        void Set(Type configType, string name, object config);

        void Load(TextReader input);

        void Save(TextWriter output);

        event EventHandler ContentChanged;
    }

    public static class ConfigStorageExtension
    {
        public static T Get<T>(this IConfigStorage storage, string name)
        {
            return (T)storage.Get(typeof(T), name);
        }

        public static void Set<T>(this IConfigStorage storage, string name, T config)
        {
            storage.Set(typeof(T), name, config!);
        }
    }
}