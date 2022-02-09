using System;

namespace Quercus.YamlConfig
{
    /// <summary>
    /// Property of this type can be used to cast config block to certain type after reading parent block
    /// </summary>
    public interface ILateBoundConfig
    {
        /// <summary>
        /// Cast contained value to given type
        /// </summary>
        /// <remarks>
        /// If config can't be converted to given type, new instance of given type is returned
        /// </remarks>
        /// <param name="type">Required type</param>
        object Cast(Type type);

        /// <summary>
        /// Assigns given config as contents of this block
        /// </summary>
        /// <param name="type">Type of config</param>
        /// <param name="config">Configuration object</param>
        void Set(Type type, object config);
    }

    public static class LateBoundConfigExtensions
    {
        public static T Cast<T>(this ILateBoundConfig config)
        {
            return (T)config.Cast(typeof(T));
        }

        public static void Set<T>(this ILateBoundConfig config, T value)
        {
            config.Set(typeof(T), value!);
        }
    }
}