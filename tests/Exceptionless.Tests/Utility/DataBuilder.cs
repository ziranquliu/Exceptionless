using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Utility;

namespace Exceptionless.Tests.Utility {
    public class DataBuilder {
        private List<EventBuilder> _events = new List<EventBuilder>();
        // TODO: stacks need to be associated to events, can't create a stack by itself
        private List<StackBuilder> _stacks = new List<StackBuilder>();
        private readonly EventPipeline _eventPipeline;
        private readonly IStackRepository _stackRepository;

        public DataBuilder(EventPipeline eventPipeline, IStackRepository stackRepository) {
            _eventPipeline = eventPipeline;
            _stackRepository = stackRepository;
        }

        public EventBuilder AddEvent(string eventType) {
            var builder = new EventBuilder(this, eventType);
            _events.Add(builder);
            return builder;
        }

        public EventBuilder AddErrorEvent() => AddEvent(Event.KnownTypes.Error);
        public EventBuilder AddLogEvent() => AddEvent(Event.KnownTypes.Log);

        public StackBuilder AddStack() {
            var builder = new StackBuilder(this);
            _stacks.Add(builder);
            return builder;
        }

        public async Task BuildAsync() {
            var events = new List<PersistentEvent>();
            foreach (var builder in _events.Cast<IDataBuilder<PersistentEvent>>()) {
                var e = new PersistentEvent();
                foreach (var m in builder.Mutations)
                    m(e);
                events.Add(e);
            }

            // create any new orgs
            // create any new projects
            // figure out how to assign the orgs and projects to the stacks and events that referenced them

            // run events through pipeline, grouped by org and project
            //await _eventPipeline.RunAsync(events, null, null);
            
            // any stacks that have mutations associated to events needs to be applied to the created stacks coming out of the pipeline

            // TODO: create data from builders
        }
    }

    public interface IDataBuilder<T> where T: class {
        ICollection<Action<T>> Mutations { get; }
    }

    public class EventBuilder : IDataBuilder<PersistentEvent> {
        private readonly ICollection<Action<PersistentEvent>> _mutations;
        private readonly DataBuilder _dataBuilder;
        private readonly Lazy<StackBuilder> _stackBuilder;
        
        public EventBuilder(DataBuilder dataBuilder, string type) {
            _dataBuilder = dataBuilder;
            _mutations = new List<Action<PersistentEvent>> {
                e => {
                    e.OrganizationId = SampleDataService.TEST_ORG_ID;
                    e.ProjectId = SampleDataService.TEST_PROJECT_ID;
                    e.Type = type;
                    e.Date = SystemClock.OffsetNow;
                }
            };
            _stackBuilder = new Lazy<StackBuilder>(() => {
                return _dataBuilder.AddStack();
            });
        }

        ICollection<Action<PersistentEvent>> IDataBuilder<PersistentEvent>.Mutations => _mutations;

        public EventBuilder Organization(string organizationId) {
            _mutations.Add(e => e.OrganizationId = organizationId);
            return this;
        }

        public EventBuilder Project(string projectId) {
            _mutations.Add(e => e.ProjectId = projectId);
            return this;
        }

        public EventBuilder Message(string message) {
            _mutations.Add(e => e.Message = message);
            return this;
        }

        public EventBuilder Source(string source) {
            _mutations.Add(e => e.Source = source);
            return this;
        }

        public EventBuilder Tag(params string[] tags) {
            _mutations.Add(e => e.Tags.AddRange(tags));
            return this;
        }

        public EventBuilder Status(StackStatus status) {
            if (status == StackStatus.Open)
                return this;

            _stackBuilder.Value.Status(status);

            return this;
        }

        public EventBuilder Fixed(DateTime? dateFixed = null) {
            _stackBuilder.Value.Fixed(dateFixed);

            return this;
        }

        public EventBuilder Fixed(string version) {
            _stackBuilder.Value.Fixed(version);
            return this;
        }

        public EventBuilder Snooze(DateTime? snoozeUntil = null) {
            _stackBuilder.Value.Snooze(snoozeUntil);
            return this;
        }
    }

    public class StackBuilder : IDataBuilder<Stack> {
        private readonly ICollection<Action<Stack>> _mutations;
        private readonly DataBuilder _dataBuilder;

        public StackBuilder(DataBuilder dataBuilder) {
            _dataBuilder = dataBuilder;
            _mutations = new List<Action<Stack>> {
                e => {
                    e.OrganizationId = SampleDataService.TEST_ORG_ID;
                    e.ProjectId = SampleDataService.TEST_PROJECT_ID;
                }
            };
        }

        ICollection<Action<Stack>> IDataBuilder<Stack>.Mutations => _mutations;

        public StackBuilder Organization(string organizationId) {
            _mutations.Add(s => s.OrganizationId = organizationId);
            return this;
        }

        public StackBuilder Project(string projectId) {
            _mutations.Add(s => s.ProjectId = projectId);
            return this;
        }

        public StackBuilder Title(string title) {
            _mutations.Add(s => s.Title = title);
            return this;
        }

        public StackBuilder Status(StackStatus status) {
            _mutations.Add(s => s.Status = status);
            return this;
        }

        public StackBuilder Fixed(DateTime? dateFixed = null) {
            _mutations.Add(s => s.DateFixed = dateFixed ?? SystemClock.UtcNow);
            return this;
        }

        public StackBuilder Fixed(string version, DateTime? dateFixed = null) {
            _mutations.Add(s => s.FixedInVersion = version);
            _mutations.Add(s => s.DateFixed = dateFixed ?? SystemClock.UtcNow);

            return this;
        }

        public StackBuilder Snooze(DateTime? snoozeUntil = null) {
            _mutations.Add(s => s.Status = StackStatus.Snoozed);
            _mutations.Add(s => s.SnoozeUntilUtc = snoozeUntil ?? SystemClock.UtcNow.AddDays(1));

            return this;
        }
    }
}
