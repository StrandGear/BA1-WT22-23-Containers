using UnityEngine;

using GD.MinMaxSlider;

public class ShredderController : MonoBehaviour, IEvent
{
    [MinMaxSlider(0, 180)]
    public Vector2Int _duration = new Vector2Int(30, 60);
    public Vector2Int Duration
    {
        get => _duration;
        set => _duration = value;
    }

    [Header("Event Info")]
    [ReadOnly]
    public float RemainingTime = 0f;

    [HideInInspector]
    EventData _data;
    public EventData Data
    {
        get => _data;
        set => _data = value;
    }

    bool _initialized = false;

    void Initialize()
    {
        // Find all AIShredder components in the scene
        var shredders = FindObjectsOfType<AIShredder>();
        foreach (var shredder in shredders)
        {
            shredder.Enabled = true;

            // Get AIBehavior component
            var ai = shredder.GetComponent<AIBehavior>();
            if (ai == null)
                Debug.LogError("AIShredder does not have AIBehavior component!");

            ai.ResetTimers();
        }

        _initialized = true;
    }

    void Update()
    {
        if (_data.Duration > 0 && !_initialized)
            Initialize();

        if (!_initialized)
            return;

        RemainingTime = _data.EndsAt - Time.time;

        if (RemainingTime <= 0)
        {
            var shredders = FindObjectsOfType<AIShredder>();
            foreach (var shredder in shredders)
            {
                shredder.Enabled = false;

                // Get AIBehavior component
                var ai = shredder.GetComponent<AIBehavior>();
                if (ai == null)
                    Debug.LogError("AIShredder does not have AIBehavior component!");

                ai.ResetToDefault();
                Destroy(gameObject);
            }
        }
    }
}
