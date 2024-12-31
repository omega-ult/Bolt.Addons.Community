using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.VisualScripting.Community
{
    public static class UnitUtility
    {
        public static string GetUnitPath(GraphReference reference, string separator = "->")
        {
            var nodePath = reference;
            var pathNames = "";
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (nodePath != null)
            {
                var prefix = "::";
                if (nodePath.graph != null)
                {
                    if (string.IsNullOrEmpty(nodePath.graph.title))
                    {
                        prefix = nodePath.graph.GetType().ToString().Split(".").Last();
                    }
                    else
                    {
                        prefix = nodePath.graph.title;
                    }

                    prefix += separator;
                }

                pathNames = prefix + pathNames; //
                nodePath = nodePath.ParentReference(false);
            }

            return pathNames;
        }
        
        public static void FocusUnit(GraphReference reference, IGraphElement unit)
        {
            // open
            GraphWindow.OpenActive(reference);

            // focus
            var context = reference.Context();
            if (context == null)
                return;
            context.BeginEdit();
            context.canvas?.ViewElements(((IGraphElement)unit).Yield());
        }
        

        public static IEnumerable<(GraphReference, Unit)> TraverseFlowGraphUnit(GraphReference graphReference)
        {
            var flowGraph = graphReference.graph as FlowGraph;
            if (flowGraph == null) yield break;
            var units = flowGraph.units;
            foreach (var element in units)
            {
                var unit = element as Unit;

                switch (unit)
                {
                    // going deep
                    case SubgraphUnit subgraphUnit:
                    {
                        var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                        if (subGraph == null) continue;
                        // find sub graph.
                        var childReference = graphReference.ChildReference(subgraphUnit, false);
                        foreach (var item in TraverseFlowGraphUnit(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case StateUnit stateUnit:
                    {
                        var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                        if (stateGraph == null) continue;
                        // find state graph.
                        var childReference = graphReference.ChildReference(stateUnit, false);
                        foreach (var item in TraverseStateGraphUnit(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    default:
                        yield return (graphReference, unit);
                        break;
                }
            }
        }
        public static IEnumerable<(GraphReference, Graph)> TraverseFlowGraph(GraphReference graphReference)
        {
            var flowGraph = graphReference.graph as FlowGraph;
            if (flowGraph == null) yield break;
            var units = flowGraph.units;
            foreach (var element in units)
            {
                var unit = element as Unit;
                switch (unit)
                {
                    // going deep
                    case SubgraphUnit subgraphUnit:
                    {
                        var subGraph = subgraphUnit.nest.embed ?? subgraphUnit.nest.graph;
                        if (subGraph == null) continue;
                        yield return (graphReference, subGraph);
                        var childReference = graphReference.ChildReference(subgraphUnit, false);
                        foreach (var item in TraverseFlowGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case StateUnit stateUnit:
                    {
                        var stateGraph = stateUnit.nest.embed ?? stateUnit.nest.graph;
                        if (stateGraph == null) continue;
                        yield return (graphReference, stateGraph);
                        var childReference = graphReference.ChildReference(stateUnit, false);
                        foreach (var item in TraverseStateGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                }
            }
        }

        // for graph node only
        public static IEnumerable<(GraphReference, Graph)> TraverseStateGraph(GraphReference graphReference)
        {
            var stateGraph = graphReference.graph as StateGraph;
            if (stateGraph == null) yield break;

            // var stateGraph = states.nest.graph;
            // yield direct graphs first.
            foreach (var state in stateGraph.states)
            {
                switch (state)
                {
                    case FlowState flowState:
                    {
                        // check flow graphs, which is the base of a state.
                        var graph = flowState.nest.embed ?? flowState.nest.graph;
                        if (graph == null) continue;
                        yield return (graphReference, graph);
                        var childReference = graphReference.ChildReference(flowState, false);
                        foreach (var item in TraverseFlowGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case SuperState superState:
                    {
                        // check state graphs
                        var subStateGraph = superState.nest.embed ?? superState.nest.graph;
                        if (subStateGraph == null) continue;
                        yield return (graphReference, subStateGraph);
                        var childReference = graphReference.ChildReference(superState, false);
                        foreach (var item in TraverseStateGraph(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case AnyState:
                        continue;
                }
            }

            // don't forget transition nodes.
            foreach (var transition in stateGraph.transitions)
            {
                if (transition is not FlowStateTransition flowStateTransition) continue;
                var graph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;
                if (graph == null) continue;
                yield return (graphReference, graph);
                var childReference = graphReference.ChildReference(flowStateTransition, false);
                foreach (var item in TraverseFlowGraph(childReference))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<(GraphReference, Unit)> TraverseStateGraphUnit(GraphReference graphReference)
        {
            var stateGraph = graphReference.graph as StateGraph;
            if (stateGraph == null) yield break;

            // yield direct graphs first.
            foreach (var state in stateGraph.states)
            {
                //Debug.Log(state);
                switch (state)
                {
                    case FlowState flowState:
                    {
                        // check flow graphs, which is the base of a state.
                        var graph = flowState.nest.embed ?? flowState.nest.graph;

                        if (graph == null) continue;
                        var childReference = graphReference.ChildReference(flowState, false);
                        foreach (var item in TraverseFlowGraphUnit(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case SuperState superState:
                    {
                        // check state graphs
                        var subStateGraph = superState.nest.embed ?? superState.nest.graph;
                        if (subStateGraph == null) continue;
                        var childReference = graphReference.ChildReference(superState, false);
                        foreach (var item in TraverseStateGraphUnit(childReference))
                        {
                            yield return item;
                        }

                        break;
                    }
                    case AnyState:
                        continue;
                }
            }
            // don't forget transition nodes.
            foreach (var transition in stateGraph.transitions)
            {
                if (transition is not FlowStateTransition flowStateTransition) continue;
                var graph = flowStateTransition.nest.embed ?? flowStateTransition.nest.graph;
                if (graph == null) continue;
                var childReference = graphReference.ChildReference(flowStateTransition, false);
                foreach (var item in TraverseFlowGraphUnit(childReference))
                {
                    yield return item;
                }
            }
        }
        
        public static string UnitBrief(IUnit unit)
        {
            if (unit == null) return null;
            switch (unit)
            {
                case FlowReroute:
                case GraphOutput:
                case GraphInput:
                case ValueReroute:
                    break;
                case Literal literal:
                    return $"{literal.value}";
                case MemberUnit invokeMember:
                    return $"{invokeMember.member.targetTypeName}->{invokeMember.member.name}";
                case UnifiedVariableUnit setVariable:
                    var vName = "";
                    if (!setVariable.name.hasValidConnection)
                    {
                        if (setVariable.defaultValues.TryGetValue(nameof(setVariable.name), out var v))
                        {
                            vName = v.ToString();
                        }
                    }
                    else
                    {
                        vName = setVariable.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{setVariable.kind}:{vName}";
                case CustomEvent customEvent:
                    var eName = "";
                    if (!customEvent.name.hasValidConnection)
                    {
                        if (customEvent.defaultValues.TryGetValue(nameof(customEvent.name), out var v))
                        {
                            eName = v.ToString();
                        }
                    }
                    else
                    {
                        eName = customEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{eName} : [{customEvent.argumentCount}]";
                case TriggerCustomEvent triggerEvent:
                    var teName = "";
                    if (!triggerEvent.name.hasValidConnection)
                    {
                        if (triggerEvent.defaultValues.TryGetValue(nameof(triggerEvent.name), out var v))
                        {
                            teName = v.ToString();
                        }
                    }
                    else
                    {
                        teName = triggerEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    return $"{teName} : [{triggerEvent.argumentCount}]";
                case TriggerDefinedEvent triggerDefinedEvent:
                    return $"{triggerDefinedEvent.eventType}";
                case TriggerGlobalDefinedEvent triggerDefinedEvent:
                    return $"{triggerDefinedEvent.eventType}";
                case GlobalDefinedEventNode definedEventNode:
                    return $"{definedEventNode.eventType}";
                case DefinedEventNode definedEventNode:
                    return $"{definedEventNode.eventType}";
                case TriggerReturnEvent triggerReturnEvent:
                    var trName = "";
                    if (!triggerReturnEvent.name.hasValidConnection)
                    {
                        if (triggerReturnEvent.defaultValues.TryGetValue(nameof(triggerReturnEvent.name), out var v))
                        {
                            trName = v.ToString();
                        }
                    }
                    else
                    {
                        trName = triggerReturnEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }
                    var trGlobal = triggerReturnEvent.global ? "[G]" : "";
                    return $"{trGlobal}{trName} : [{triggerReturnEvent.count}]";
                case ReturnEvent returnEvent:
                    var rName = "";
                    if (!returnEvent.name.hasValidConnection)
                    {
                        if (returnEvent.defaultValues.TryGetValue(nameof(returnEvent.name), out var v))
                        {
                            rName = v.ToString();
                        }
                    }
                    else
                    {
                        rName = returnEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }

                    var rGlobal = returnEvent.global ? "[G]" : "";
                    return $"{rGlobal}{rName} : [{returnEvent.count}]";
                case BoltUnityEvent boltUnityEvent:
                    var uEvent = "";
                    if (!boltUnityEvent.name.hasValidConnection)
                    {
                        if (boltUnityEvent.defaultValues.TryGetValue(nameof(boltUnityEvent.name), out var v))
                        {
                            uEvent = v.ToString();
                        }
                    }
                    else
                    {
                        uEvent = boltUnityEvent.name.connection.source.unit.ToString().Split('#')[0];
                    }
                    return $"{uEvent}";
                case MissingType missingType:
                    return $"{missingType.formerType}";
                default:
                    return null;
            }

            return null;
        }
    }
}