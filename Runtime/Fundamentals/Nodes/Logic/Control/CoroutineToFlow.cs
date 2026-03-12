using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[UnitTitle("CoroutineToFlow")]//Unit title
[UnitCategory("Community\\Control")]
[TypeIcon(typeof(Flow))]//Unit icon
public class CorutineConverter : Unit
{
    
    [DoNotSerialize]
    public ControlInput In;
    [DoNotSerialize]
    public ControlOutput Converted;
    [DoNotSerialize]
    public ControlOutput Corutine;

    [SerializeAs(nameof(argumentCount))]
    private int _argumentCount;

    [DoNotSerialize]
    [Inspectable, UnitHeaderInspectable("Arguments")]
    public int argumentCount
    {
        get => _argumentCount;
        set => _argumentCount = Mathf.Clamp(value, 0, 10);
    }

    [SerializeAs(nameof(argumentTypes))]
    private List<Type> _argumentTypes;

    [Inspectable]
    public List<Type> argumentTypes
    {
        get => _argumentTypes;
        set
        {
            value ??= new List<Type>();
            _argumentTypes = value;
        }
    }

    [DoNotSerialize]
    public List<ValueInput> argumentInputPorts { get; } = new List<ValueInput>();
       
    [DoNotSerialize]
    public List<ValueOutput> argumentOutputPorts { get; } = new List<ValueOutput>();

    protected override void Definition()
    {
        In = ControlInput("In", Convert);
        Converted = ControlOutput("Flow");
        Corutine = ControlOutput("Coroutine");

        argumentInputPorts.Clear();
        argumentOutputPorts.Clear();

        for (var i = 0; i < argumentCount; i++)
        {
            var type = (argumentTypes != null && i < argumentTypes.Count && argumentTypes[i] != null) 
                ? argumentTypes[i] 
                : typeof(object);
                
            // Input port
            var inputKey = "inputArg_" + i;
            argumentInputPorts.Add(ValueInput(type, inputKey));

            // Output port
            var outputKey = "outputArg_" + i;
            argumentOutputPorts.Add(ValueOutput(type, outputKey));
        }

        Succession(In, Converted);
        Succession(In, Corutine);
    }

    private ControlOutput Convert(Flow flow) 
    {
        var GraphRef = flow.stack.ToReference();

        // Capture inputs
        var capturedArgs = new object[argumentCount];
        for (int i = 0; i < argumentCount; i++)
        {
            capturedArgs[i] = flow.GetValue(argumentInputPorts[i]);
        }

        if (!flow.isCoroutine)
        {
            Debug.LogWarning("CoroutineToFlow node is used to convert a Corutine flow to a normal flow there is no point in using it in a regular flow", flow.stack.gameObject);
            
            for (int i = 0; i < argumentCount; i++)
            {
                flow.SetValue(argumentOutputPorts[i], capturedArgs[i]);
            }
            return Converted;
        }
        else 
        {
            var Convertedflow = Flow.New(GraphRef);
            
            for (int i = 0; i < argumentCount; i++)
            {
                // Set value for new flow (normal)
                Convertedflow.SetValue(argumentOutputPorts[i], capturedArgs[i]);
                // Set value for original flow (coroutine pass through)
                flow.SetValue(argumentOutputPorts[i], capturedArgs[i]);
            }

            Convertedflow.Run(Converted);
            return Corutine;
        }
    }
}
