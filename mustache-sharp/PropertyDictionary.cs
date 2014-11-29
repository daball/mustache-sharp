﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace Mustache
{
    /// <summary>
    /// Provides methods for creating instances of PropertyDictionary.
    /// </summary>
    internal sealed class PropertyDictionary : IDictionary<string, object>
    {
        private static readonly Dictionary<Type, Dictionary<string, Func<object, object>>> _cache = new Dictionary<Type, Dictionary<string, Func<object, object>>>();

        private readonly object _instance;
        private readonly bool _isDynamicObject;
        private readonly Dictionary<string, Func<object, object>> _typeCache;

        /// <summary>
        /// Initializes a new instance of a PropertyDictionary.
        /// </summary>
        /// <param name="instance">The instance to wrap in the PropertyDictionary.</param>
        public PropertyDictionary(object instance)
        {
            _instance = instance;
            if (instance == null)
            {
                _typeCache = new Dictionary<string, Func<object, object>>();
            }
            else
            {
                _isDynamicObject = isDynamicObject(_instance);
                lock (_cache)
                {
                    _typeCache = getCacheType(_instance);
                }
            }
        }

        private static Dictionary<string, Func<object, object>> getCacheType(object instance)
        {
            Type type = instance.GetType();
            Dictionary<string, Func<object, object>> typeCache;
            if (!_cache.TryGetValue(type, out typeCache))
            {
                typeCache = new Dictionary<string, Func<object, object>>();
                
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
                
                var properties = getMembers(type, type.GetProperties(flags).Where(p => !p.IsSpecialName));
                foreach (PropertyInfo propertyInfo in properties)
                {
                    typeCache.Add(propertyInfo.Name, i => propertyInfo.GetValue(i, null));
                }

                var fields = getMembers(type, type.GetFields(flags).Where(f => !f.IsSpecialName));
                foreach (FieldInfo fieldInfo in fields)
                {
                    typeCache.Add(fieldInfo.Name, i => fieldInfo.GetValue(i));
                }

                //Disable cache of dynamic members, does not refresh for mutated objects of the same base type derived from DynamicObject:
                //var dynamicMembers = getDynamicMembers(instance);
                //foreach (KeyValuePair<string, object> member in dynamicMembers)
                //{
                //    typeCache[member.Key] = (i => member.Value);
                //}
                
                _cache.Add(type, typeCache);
            }
            return typeCache;
        }

        private static bool isDynamicObject(object instance)
        {
            Type type = instance.GetType();
            Type baseType = type.BaseType;
            bool isDynamicObject = false;

            while (baseType.BaseType != null)
            {
                isDynamicObject = (baseType.FullName == "System.Dynamic.DynamicObject");
                if (isDynamicObject)
                    break;
                else
                    baseType = baseType.BaseType;
            }

            return isDynamicObject;
        }

        private static IEnumerable<KeyValuePair<string, Func<object, object>>> getDynamicMembers(object instance)
        {
            Dictionary<string, Func<object, object>> members = new Dictionary<string, Func<object, object>>();
            DynamicObject dynamicInstance = (DynamicObject)instance;
            foreach (string member in dynamicInstance.GetDynamicMemberNames())
            {
                members.Add(member, getDynamicMemberValueGetter(instance, member));
            }
            return members;
        }

        private static Func<object, object> getDynamicMemberValueGetter(object instance, string memberName)
        {
            DynamicObject dynamicInstance = (DynamicObject)instance;
            return i => Dynamitey.Dynamic.InvokeGet(i, memberName);
        }

        private static IEnumerable<TMember> getMembers<TMember>(Type type, IEnumerable<TMember> members)
            where TMember : MemberInfo
        {
            var singles = from member in members
                          group member by member.Name into nameGroup
                          where nameGroup.Count() == 1
                          from property in nameGroup
                          select property;
            var multiples = from member in members
                            group member by member.Name into nameGroup
                            where nameGroup.Count() > 1
                            select
                            (
                                from member in nameGroup
                                orderby getDistance(type, member)
                                select member
                            ).First();
            var combined = singles.Concat(multiples);
            return combined;
        }

        private static int getDistance(Type type, MemberInfo memberInfo)
        {
            int distance = 0;
            for (; type != null && type != memberInfo.DeclaringType; type = type.BaseType)
            {
                ++distance;
            }
            return distance;
        }

        /// <summary>
        /// Gets the underlying instance.
        /// </summary>
        public object Instance
        {
            get { return _instance; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        void IDictionary<string, object>.Add(string key, object value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Determines whether a property with the given name exists.
        /// </summary>
        /// <param name="key">The name of the property.</param>
        /// <returns>True if the property exists; otherwise, false.</returns>
        public bool ContainsKey(string key)
        {
            if (_isDynamicObject)
                return ((DynamicObject)_instance).GetDynamicMemberNames().Contains(key);
            else
                return _typeCache.ContainsKey(key);
        }

        /// <summary>
        /// Gets the name of the properties in the type.
        /// </summary>
        public ICollection<string> Keys
        {
            get
            {
                if (_isDynamicObject)
                    return ((DynamicObject)_instance).GetDynamicMemberNames().ToArray();
                else
                    return _typeCache.Keys;
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary<string, object>.Remove(string key)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Tries to get the value for the given property name.
        /// </summary>
        /// <param name="key">The name of the property to get the value for.</param>
        /// <param name="value">The variable to store the value of the property or the default value if the property is not found.</param>
        /// <returns>True if a property with the given name is found; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">The name of the property was null.</exception>
        public bool TryGetValue(string key, out object value)
        {
            Func<object, object> getter;
            if (_isDynamicObject)
            {
                if (!((DynamicObject)_instance).GetDynamicMemberNames().Contains(key))
                {
                    value = null;
                    return false;
                }
                getter = getDynamicMemberValueGetter(_instance, key);
            }
            else
            {
                if (!_typeCache.TryGetValue(key, out getter))
                {
                    value = null;
                    return false;
                }
            }
            value = getter(_instance);
            return true;
        }

        /// <summary>
        /// Gets the values of all of the properties in the object.
        /// </summary>
        public ICollection<object> Values
        {
            get
            {
                List<object> values = new List<object>();
                if (_isDynamicObject)
                {
                    foreach (KeyValuePair<string, Func<object, object>> member in getDynamicMembers(_instance))
                    {
                        object value = member.Value(_instance);
                        values.Add(value);
                    }
                }
                else
                {
                    ICollection<Func<object, object>> getters = _typeCache.Values;
                    foreach (Func<object, object> getter in getters)
                    {
                        object value = getter(_instance);
                        values.Add(value);
                    }
                }
                return values.AsReadOnly();
            }
        }

        /// <summary>
        /// Gets or sets the value of the property with the given name.
        /// </summary>
        /// <param name="key">The name of the property to get or set.</param>
        /// <returns>The value of the property with the given name.</returns>
        /// <exception cref="System.ArgumentNullException">The property name was null.</exception>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">The type does not have a property with the given name.</exception>
        /// <exception cref="System.ArgumentException">The property did not support getting or setting.</exception>
        /// <exception cref="System.ArgumentException">
        /// The object does not match the target type, or a property is a value type but the value is null.
        /// </exception>
        public object this[string key]
        {
            get
            {
                if (_isDynamicObject)
                {
                    Func<object, object> getter = getDynamicMemberValueGetter(_instance, key);
                    return getter(_instance);
                }
                else
                {
                    Func<object, object> getter = _typeCache[key];
                    return getter(_instance);
                }
            }
            [EditorBrowsable(EditorBrowsableState.Never)]
            set
            {
                throw new NotSupportedException();
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            throw new NotSupportedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection<KeyValuePair<string, object>>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            Func<object, object> getter;
            if (_isDynamicObject)
            {
                if (!((DynamicObject)_instance).GetDynamicMemberNames().Contains(item.Key))
                {
                    return false;
                }
                getter = getDynamicMemberValueGetter(_instance, item.Key);
            }
            else
            {
                if (!_typeCache.TryGetValue(item.Key, out getter))
                {
                    return false;
                }
            }
            object value = getter(_instance);
            return Equals(item.Value, value);
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            List<KeyValuePair<string, object>> pairs = new List<KeyValuePair<string, object>>();
            ICollection<KeyValuePair<string, Func<object, object>>> collection = _typeCache;
            foreach (KeyValuePair<string, Func<object, object>> pair in collection)
            {
                Func<object, object> getter = pair.Value;
                object value = getter(_instance);
                pairs.Add(new KeyValuePair<string, object>(pair.Key, value));
            }
            pairs.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Gets the number of properties in the type.
        /// </summary>
        public int Count
        {
            get
            {
                if (_isDynamicObject)
                    return ((DynamicObject)_instance).GetDynamicMemberNames().Count();
                else
                    return _typeCache.Count;
            }
        }

        /// <summary>
        /// Gets or sets whether updates will be ignored.
        /// </summary>
        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
        {
            get { return true; }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Gets the property name/value pairs in the object.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            if (_isDynamicObject)
            {
                foreach (KeyValuePair<string, Func<object, object>> pair in getDynamicMembers(_instance))
                {
                    Func<object, object> getter = pair.Value;
                    object value = getter(_instance);
                    yield return new KeyValuePair<string, object>(pair.Key, value);
                }
            }
            else
            {
                foreach (KeyValuePair<string, Func<object, object>> pair in _typeCache)
                {
                    Func<object, object> getter = pair.Value;
                    object value = getter(_instance);
                    yield return new KeyValuePair<string, object>(pair.Key, value);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}