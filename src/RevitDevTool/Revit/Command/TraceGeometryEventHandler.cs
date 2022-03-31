using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitDevTool.Revit.Command
{
    public sealed class TraceGeometryEventHandler : IExternalEventHandler
    {
        internal Queue<Action<UIApplication>> Actions { get; set; }
        private static readonly Lazy<TraceGeometryEventHandler> _lazy = new Lazy<TraceGeometryEventHandler>(() => new TraceGeometryEventHandler());
        private ExternalEvent _externalEvent;
        public static TraceGeometryEventHandler Instance => _lazy.Value;

        private TraceGeometryEventHandler()
        {
            Actions = new Queue<Action<UIApplication>>();
        }

        public void Invoke(Action<UIApplication> action)
        {
            Actions.Enqueue(action);
            _externalEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            while (this.Actions.Any())
            {
                var action = this.Actions.Dequeue();
                action.Invoke(app);
            }
        }

        public string GetName()
        {
            return "TraceGeometry";
        }

        public void Start()
        {
            if (_externalEvent==null)
            {
                _externalEvent = ExternalEvent.Create(Instance);
            }
        }
    }
}
