using System;
using System.Collections.Generic;
using System.Linq;
using Fohjin.DDD.Bus;
using Fohjin.DDD.CommandHandlers;
using Fohjin.DDD.Commands;
using Fohjin.DDD.Domain;
using Fohjin.DDD.Domain.Entities.Mementos;
using Fohjin.DDD.Events;
using Fohjin.EventStorage;
using Moq;
using NUnit.Framework;
using StructureMap;

namespace Test.Fohjin.DDD.Domain
{
    [Specification]
    public abstract class CommandTestFixture<TCommand, TCommandHandler, TAggregateRoot> 
        where TCommand : class, ICommand, IMessage
        where TCommandHandler : class, ICommandHandler<TCommand>
        where TAggregateRoot : IOrginator, IEventProvider, new()
    {
        protected TAggregateRoot aggregateRoot;
        protected ICommandHandler<TCommand> commandHandler;
        protected Exception caught;
        protected IEnumerable<IDomainEvent> events;

        protected abstract IEnumerable<IDomainEvent> Given();
        protected abstract TCommand When();

        [SetUp]
        public void Setup()
        {
            caught = new NullException();
            aggregateRoot = new TAggregateRoot();
            aggregateRoot.LoadFromHistory(Given());

            commandHandler = BuildCommandHandler();

            try
            {
                commandHandler.Execute(When());
                events = aggregateRoot.GetChanges();
            }
            catch (Exception e)
            {
                caught = e;
            }
        }

        private ICommandHandler<TCommand> BuildCommandHandler()
        {
            var constructorInfo = typeof(TCommandHandler).GetConstructors().First();

            IContainer container = new Container();

            foreach (var parameter in constructorInfo.GetParameters())
            {
                if (parameter.ParameterType == typeof(IRepository))
                {
                    var repositoryMock = new Mock<IRepository>();
                    repositoryMock.Setup(x => x.GetById<TAggregateRoot>(It.IsAny<Guid>())).Returns(aggregateRoot);
                    repositoryMock.Setup(x => x.Save(It.IsAny<TAggregateRoot>())).Callback<TAggregateRoot>(x => aggregateRoot = x);
                    container.Inject(parameter.ParameterType, repositoryMock.Object);
                    continue;
                }

                container.Inject(parameter.ParameterType, CreateMock(parameter.ParameterType));
            }

            return container.GetInstance<TCommandHandler>();
        }

        private static object CreateMock(Type type)
        {
            var constructorInfo = typeof (Mock<>).MakeGenericType(type).GetConstructors().First();
            return constructorInfo.Invoke(new object[]{});
        }
    }

    public class NullException : Exception {}

    public class DomainEvent
    {
        public static EventVersionSetter Set(IDomainEvent domainEvent)
        {
            return new EventVersionSetter(domainEvent);
        }
    }

    public class EventVersionSetter
    {
        private readonly IDomainEvent _domainEvent;

        public EventVersionSetter(IDomainEvent domainEvent)
        {
            _domainEvent = domainEvent;
        }

        public IDomainEvent ToVersion(int version)
        {
            _domainEvent.Version = version;
            return _domainEvent;
        }
    }
}