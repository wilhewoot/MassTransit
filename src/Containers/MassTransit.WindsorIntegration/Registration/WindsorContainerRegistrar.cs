// Copyright 2007-2019 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.
namespace MassTransit.WindsorIntegration.Registration
{
    using Castle.MicroKernel.Registration;
    using Castle.Windsor;
    using Courier;
    using MassTransit.Registration;
    using Saga;
    using ScopeProviders;
    using Scoping;


    public class WindsorContainerRegistrar :
        IContainerRegistrar
    {
        readonly IWindsorContainer _container;

        public WindsorContainerRegistrar(IWindsorContainer container)
        {
            _container = container;
        }

        public void RegisterConsumer<T>()
            where T : class, IConsumer
        {
            _container.Register(
                Component.For<T>()
                    .LifestyleScoped());
        }

        public void RegisterSaga<T>()
            where T : class, ISaga
        {
        }

        public void RegisterExecuteActivity<TActivity, TArguments>()
            where TActivity : class, ExecuteActivity<TArguments>
            where TArguments : class
        {
            RegisterActivityIfNotPresent<TActivity>();

            _container.Register(
                Component.For<IExecuteActivityScopeProvider<TActivity, TArguments>>()
                    .ImplementedBy<WindsorExecuteActivityScopeProvider<TActivity, TArguments>>());
        }

        public void RegisterCompensateActivity<TActivity, TLog>()
            where TActivity : class, CompensateActivity<TLog>
            where TLog : class
        {
            RegisterActivityIfNotPresent<TActivity>();

            _container.Register(
                Component.For<ICompensateActivityScopeProvider<TActivity, TLog>>()
                    .ImplementedBy<WindsorCompensateActivityScopeProvider<TActivity, TLog>>());
        }

        void RegisterActivityIfNotPresent<TActivity>()
            where TActivity : class
        {
            if (!_container.Kernel.HasComponent(typeof(TActivity)))
                _container.Register(Component.For<TActivity>().LifestyleScoped());
        }
    }
}
