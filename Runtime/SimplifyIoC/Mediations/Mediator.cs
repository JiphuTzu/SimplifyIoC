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
 * @class SimplifyIoC.Mediations.Mediator
 * 
 * Base class for all Mediators.
 * 
 * @see SimplifyIoC.Mediations.IMediationBinder
 */

using SimplifyIoC.Contexts;
using SimplifyIoC.Injectors;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public class Mediator : MonoBehaviour, IMediator
    {
	    [Inject]
	    public IInjectionBinder injectionBinder { get; set; }
        [Inject(ContextKeys.CONTEXT_VIEW)]
        public ContextView contextView { get; set; }

        //public Mediator() { }

        /**
		 * Fires directly after creation and before injection
		 */
        public virtual void PreRegister() { }

        /**
		 * Fires after all injections satisfied.
		 *
		 * Override and place your initialization code here.
		 */
        public virtual void OnRegister() { }

        /**
		 * Fires on removal of view.
		 *
		 * Override and place your cleanup code here
		 */
        public virtual void OnRemove() { }

        /**
		 * Fires on enabling of view.
		 */
        public virtual void OnEnabled() { }

        /**
		 * Fires on disabling of view.
		 */
        public virtual void OnDisabled() { }
    }
}