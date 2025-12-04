/*
 * Copyright 2013 ThirdMotion, Inc.
 *
 *	Licensed under the Apache License, Version 2.0 (the "License");
 *	you may not use this file except in compliance with the License.
 *	You may obtain a copy of the License at
 *
 *		http://www.apache.org/licenses/LICENSE-2.0
 *
 *		Unless required by applicable law or agreed to in writing, software
 *		distributed under the License is distributed on an "AS IS" BASIS,
 *		WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *		See the License for the specific language governing permissions and
 *		limitations under the License.
 */

/*
 * @class SimplifyIoC.Framework.Binder
 * 
 * Collection class for bindings.
 * 
 * Binders are a collection class (akin to ArrayList and Dictionary)
 * with the specific purpose of connecting lists of things that are
 * not necessarily related, but need some type of runtime association.
 * Binders are the core concept of the StrangeIoC framework, allowing
 * all the other functionality to exist and further functionality to
 * easily be created.
 * 
 * Think of each Binder as a collection of causes and effects, or actions
 * and reactions. If the Key action happens, it triggers the Value
 * action. So, for example, an Event may be the Key that triggers
 * instantiation of a particular class.
 */

/*
 * @class SimplifyIoC.Framework.Binder
 *
 * 用于绑定的集合类。
 *
 * Binder（绑定器）是一种集合类（类似于 ArrayList 和 Dictionary），
 * 其特定目的是连接那些不一定相关但需要在运行时建立某种关联的列表。
 * Binder 是 StrangeIoC 框架的核心概念，它使得所有其他功能得以存在，
 * 并且可以轻松创建更多功能。
 *
 * 可以将每个 Binder 视为一系列原因与结果，或动作与反应的集合。
 * 如果键（Key）动作发生，就会触发值（Value）动作。
 * 例如，一个事件（Event）可以作为键，触发特定类的实例化。
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SimplifyIoC.Framework
{
    public enum BindingConst
    {
        /// Null is an acceptable binding, but dictionaries choke on it, so we map null to this instead.
        NULLOID
    }
    public class Binder : IBinder
    {
        /// Dictionary of all bindings
        /// Two-layer keys. First key to individual Binding keys,
        /// then to Binding names. (This wouldn't be required if
        /// Unity supported Tuple or HashSet.)
        protected readonly Dictionary<object, Dictionary<object, IBinding>> bindings = new();

        private readonly Dictionary<object, Dictionary<IBinding, object>> _conflicts = new();

        protected List<object> bindingWhitelist;

        /// A handler for resolving the nature of a binding during chained commands
        public delegate void BindingResolver(IBinding binding);

        public virtual IBinding Bind<T>()
        {
            return Bind(typeof(T));
        }

        public virtual IBinding Bind(object key)
        {
            var binding = GetRawBinding();
            binding.Bind(key);
            return binding;
        }

        public virtual IBinding GetBinding<T>()
        {
            return GetBinding(typeof(T), null);
        }

        public virtual IBinding GetBinding(object key)
        {
            return GetBinding(key, null);
        }

        public virtual IBinding GetBinding<T>(object name)
        {
            return GetBinding(typeof(T), name);
        }

        public virtual IBinding GetBinding(object key, object name)
        {
            if (_conflicts.Count > 0)
            {
                var conflictSummary = "";
                var keys = _conflicts.Keys;
                foreach (var k in keys)
                {
                    if (conflictSummary.Length > 0)
                    {
                        conflictSummary += ", ";
                    }
                    conflictSummary += k.ToString();
                }
                throw new Exception("Binder cannot fetch Bindings when the binder is in a conflicted state.\nConflicts: " + conflictSummary);
            }

            if (!bindings.TryGetValue(key, out var dict)) return null;
            name ??= BindingConst.NULLOID;
            return dict.GetValueOrDefault(name);
        }

        public virtual void Unbind<T>()
        {
            Unbind(typeof(T), null);
        }

        public virtual void Unbind(object key)
        {
            Unbind(key, null);
        }

        public virtual void Unbind<T>(object name)
        {
            Unbind(typeof(T), name);
        }

        public virtual void Unbind(object key, object name)
        {
            Debug.Log($"{this}.Unbind({key}, {name})");
            if (!bindings.TryGetValue(key, out var dict)) return;
            var bindingName = name ?? BindingConst.NULLOID;
            dict.Remove(bindingName);
        }

        public virtual void Unbind(IBinding binding)
        {
            if (binding == null) return;
            Unbind(binding.key, binding.name);
        }

        public virtual void RemoveValue(IBinding binding, object value)
        {
            if (binding == null || value == null) return;
            
            var key = binding.key;
            if (!bindings.TryGetValue(key, out var dict) || dict.ContainsKey(binding.name)) return;
            var useBinding = dict[binding.name];
            useBinding.RemoveValue(value);

            //If result is empty, clean it out
            var values = useBinding.value as object[];
            if (values == null || values.Length == 0)
            {
                dict.Remove(useBinding.name);
            }
        }

        public virtual void RemoveKey(IBinding binding, object key)
        {
            if (binding == null || key == null || !bindings.TryGetValue(key, out var dict))
            {
                return;
            }

            if (!dict.TryGetValue(binding.name, out var useBinding)) return;
            useBinding.RemoveKey(key);
            var keys = useBinding.key as object[];
            if (keys != null && keys.Length == 0)
            {
                dict.Remove(binding.name);
            }
        }

        public virtual void RemoveName(IBinding binding, object name)
        {
            if (binding == null || name == null)
            {
                return;
            }
            object key;
            if (binding.keyConstraint.Equals(BindingConstraintType.ONE))
            {
                key = binding.key;
            }
            else
            {
                var keys = binding.key as object[];
                key = keys[0];
            }

            var dict = bindings[key];
            if (!dict.TryGetValue(name, out var useBinding)) return;
            useBinding.RemoveName(name);
        }

        public virtual IBinding GetRawBinding()
        {
            return new Binding(Resolver);
        }

        /// The default handler for resolving bindings during chained commands
        protected virtual void Resolver(IBinding binding)
        {
            var key = binding.key;
            if (binding.keyConstraint.Equals(BindingConstraintType.ONE))
            {
                ResolveBinding(binding, key);
            }
            else
            {
                var keys = key as object[];
                var aa = keys.Length;
                for (var a = 0; a < aa; a++)
                {
                    ResolveBinding(binding, keys[a]);
                }
            }
        }

        /**
		 * This method places individual Bindings into the bindings Dictionary
		 * as part of the resolving process. Note that while some Bindings
		 * may store multiple keys, each key takes a unique position in the
		 * bindings Dictionary.
		 * 
		 * Conflicts in the course of fluent binding are expected, but GetBinding
		 * will throw an error if there are any unresolved conflicts.
		 */
        public virtual void ResolveBinding(IBinding binding, object key)
        {

            //Check for existing conflicts
            if (_conflicts.TryGetValue(key, out var inConflict)) //does the current key have any conflicts?
            {
                if (inConflict.TryGetValue(binding, out var conflictName)) //Am I on the conflict list?
                {
                    if (IsConflictCleared(inConflict, binding)) //Am I now out of conflict?
                    {
                        ClearConflict(key, conflictName, inConflict); //remove all from conflict list.
                    }
                    else
                    {
                        return; //still in conflict
                    }
                }
            }

            //Check for and assign new conflicts
            var bindingName = binding.name ?? BindingConst.NULLOID;
            Dictionary<object, IBinding> dict;
            if ((bindings.TryGetValue(key, out var binding1)))
            {
                dict = binding1;
                //Will my registration create a new conflict?
                if (dict.ContainsKey(bindingName))
                {

                    //If the existing binding is not this binding, and the existing binding is not weak
                    //If it IS weak, we will proceed normally and overwrite the binding in the dictionary
                    var existingBinding = dict[bindingName];
                    //if (existingBinding != binding && !existingBinding.isWeak)
                    //SDM2014-01-20: as part of cross-context implicit bindings fix, attempts by a weak binding to replace a non-weak binding are ignored instead of being 
                    if (existingBinding != binding)
                    {
                        if (!existingBinding.isWeak && !binding.isWeak)
                        {
                            //register both conflictees
                            RegisterNameConflict(key, binding, dict[bindingName]);
                            return;
                        }

                        if (existingBinding.isWeak && (!binding.isWeak || existingBinding.value == null || existingBinding.value is System.Type))
                        {
                            //SDM2014-01-20: (in relation to the cross-context implicit bindings fix)
                            // 1) if the previous binding is weak and the new binding is not weak, then the new binding replaces the previous;
                            // 2) but if the new binding is also weak, then it only replaces the previous weak binding if the previous binding
                            // has not already been instantiated:

                            //Remove the previous binding.
                            dict.Remove(bindingName);
                        }
                    }

                }
            }
            else
            {
                dict = new Dictionary<object, IBinding>();
                bindings[key] = dict;
            }

            //Remove nulloid bindings
            if (dict.ContainsKey(BindingConst.NULLOID) && dict[BindingConst.NULLOID].Equals(binding))
            {
                dict.Remove(BindingConst.NULLOID);
            }

            //Add (or override) our new binding!
            dict.TryAdd(bindingName, binding);
        }

        /// <summary>
        /// For consumed bindings, provide a secure whitelist of legal bindings.
        /// </summary>
        /// <param name="list"> A List of fully-qualified classnames eligible to be consumed during dynamic runtime binding.</param>
        public virtual void WhitelistBindings(List<object> list)
        {
            bindingWhitelist = list;
        }

        /// <summary>
        /// Override this method in subclasses to add special-case SYNTACTICAL SUGAR for Runtime JSON bindings.
        /// For example, if your Binder needs a special JSON tag BindView, such that BindView is simply
        /// another way of expressing 'Bind', override this method conform the sugar to
        /// match the base definition (BindView becomes Bind).
        /// </summary>
        /// <returns>The conformed Dictionary.</returns>
        /// <param name="dictionary">A Dictionary representing the options for a Binding.</param>
        protected virtual Dictionary<string, object> ConformRuntimeItem(Dictionary<string, object> dictionary)
        {
            return dictionary;
        }

        /// <summary>
        /// Performs the key value bindings for a JSON runtime binding.
        /// </summary>
        /// <returns>A Binding.</returns>
        /// <param name="keyList">A list of things to Bind.</param>
        /// <param name="valueList">A list of the things to which we're binding.</param>
        // protected virtual IBinding PerformKeyValueBindings(List<object> keyList, List<object> valueList)
        // {
        //     IBinding binding = null;
        //
        //     // Bind in order
        //     foreach (var key in keyList)
        //     {
        //         binding = Bind(key);
        //     }
        //     foreach (var value in valueList)
        //     {
        //         binding = binding.To(value);
        //     }
        //
        //     return binding;
        // }

        /// Take note of bindings that are in conflict.
        /// This occurs routinely during fluent binding, but will spark an error if
        /// GetBinding is called while this Binder still has conflicts.
        protected void RegisterNameConflict(object key, IBinding newBinding, IBinding existingBinding)
        {
            Dictionary<IBinding, object> dict;
            if (!_conflicts.TryGetValue(key, out var conflict))
            {
                dict = new Dictionary<IBinding, object>();
                _conflicts[key] = dict;
            }
            else
            {
                dict = conflict;
            }
            dict[newBinding] = newBinding.name;
            dict[existingBinding] = newBinding.name;
        }

        /// Returns true if the provided binding and the binding in the dict are no longer conflicting
        protected bool IsConflictCleared(Dictionary<IBinding, object> dict, IBinding binding)
        {
            foreach (var kv in dict)
            {
                if (kv.Key != binding && kv.Key.name == binding.name)
                {
                    return false;
                }
            }
            return true;
        }

        protected void ClearConflict(object key, object name, Dictionary<IBinding, object> dict)
        {
            var removalList = new List<IBinding>();

            foreach (var kv in dict)
            {
                var v = kv.Value;
                if (v.Equals(name))
                {
                    removalList.Add(kv.Key);
                }
            }
            var aa = removalList.Count;
            for (var a = 0; a < aa; a++)
            {
                dict.Remove(removalList[a]);
            }
            if (dict.Count == 0)
            {
                _conflicts.Remove(key);
            }
        }

        protected T[] SpliceValueAt<T>(int splicePos, object[] objectValue)
        {
            var newList = new T[objectValue.Length - 1];
            var mod = 0;
            var aa = objectValue.Length;
            for (var a = 0; a < aa; a++)
            {
                if (a == splicePos)
                {
                    mod = -1;
                    continue;
                }
                newList[a + mod] = (T)objectValue[a];
            }
            return newList;
        }

        /// Remove the item at splicePos from the list objectValue 
        protected object[] SpliceValueAt(int splicePos, object[] objectValue)
        {
            return SpliceValueAt<object>(splicePos, objectValue);
        }

        public virtual void OnRemove() { }
    }
}