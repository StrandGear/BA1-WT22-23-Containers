using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGrab : MonoBehaviour
{
    [Header("Grab Settings")]
    [Range(0f, 10f)]
    public float AutoGrabRadius = 1f;
    public float ContainerDistanceWeight = 0.5f;
    public float MouseDirectionWeight = 0.5f;

    [Header("Air Movement Settings")]
    public float FloatAmount = 3f;

    [Range(0f, 1000f)]
    public float Acceleration = 500f;

    [Range(0f, 10f)]
    public float DecelerationMultiplier = 3.5f;

    [HideInInspector]
    public GameObject GrabbedObject;

    GameObject _lastClosestObject = null;
    PlayerLocomotion _locomotion;
    ContainerGeneric _grabbedContainer;

    // For Animation
    Transform _playerModel;
    Animator _playerAnimator;

    void Start()
    {
        _playerModel = transform.Find("Jeffrey");
        _playerAnimator = _playerModel.GetComponent<Animator>();

        if (_playerModel == null || _playerAnimator == null)
        {
            Debug.LogError("No player model found or player animator");
        }

        _locomotion = GetComponent<PlayerLocomotion>();
        if (_locomotion == null)
            Debug.LogError("PlayerLocomotion script not found on player");
    }

    void OnValidate()
    {
        _locomotion = GetComponent<PlayerLocomotion>();
        if (_locomotion == null)
            Debug.LogError("PlayerLocomotion script not found on player");
    }

    void Update()
    {
        GameObject closestObject = ScanForGrabbables(AutoGrabRadius);

        // Remove outline from the previous closest object
        if (closestObject != null && closestObject != _lastClosestObject)
        {
            var outlines = closestObject.GetComponentsInChildren<Outline>();
            foreach (var outline in outlines)
                outline.enabled = true;

            if (_lastClosestObject != null)
            {
                var lastOutlines = _lastClosestObject.GetComponentsInChildren<Outline>();
                foreach (var outline in lastOutlines)
                    outline.enabled = false;
            }

            _lastClosestObject = closestObject;
        }
        else if (closestObject != null && _lastClosestObject == closestObject)
        {
            var outlines = closestObject.GetComponentsInChildren<Outline>();
            foreach (var outline in outlines)
                outline.enabled = true;
        }
        else if (closestObject == null && _lastClosestObject != null)
        {
            var outlines = _lastClosestObject.GetComponentsInChildren<Outline>();
            foreach (var outline in outlines)
                outline.enabled = false;
        }

        if (Input.GetKeyDown(KeyCode.E) && !_locomotion.OnSecondFloor && closestObject != null && GrabbedObject == null)
        {
                _playerAnimator.SetBool("IsGrabbing", true);

                GrabbedObject = closestObject;
                GrabbedObject.transform.SetParent(transform);

                // Set direction to object
                _locomotion.Direction = GrabbedObject.transform.position - transform.position;

                // Disable collisions only with the player and disable gravity
                Physics.IgnoreCollision(GrabbedObject.GetComponent<Collider>(), GetComponent<Collider>());
                GrabbedObject.GetComponent<Rigidbody>().useGravity = false;
        }

        if (Input.GetKeyDown(KeyCode.X) && GrabbedObject != null)
        {
            // Enable collisions with the player and gravity
            Physics.IgnoreCollision(GrabbedObject.GetComponent<Collider>(), GetComponent<Collider>(), false);
            GrabbedObject.GetComponent<Rigidbody>().useGravity = true;

            GrabbedObject.transform.SetParent(null);
            GrabbedObject = null;
        }


        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = new Vector3(-2f, 0f, 0f);
        }
    }

    void FixedUpdate()
    {
        if (GrabbedObject != null)
        {
            MoveObject();

            if (_grabbedContainer == null)
                _grabbedContainer = GrabbedObject.GetComponent<ContainerGeneric>();

            if (_grabbedContainer != null)
                _grabbedContainer.RenderDecal();
        }

        _grabbedContainer = null;
    }

    void OnDrawGizmos()
    {
        DebugVisuals();
    }

    GameObject ScanForGrabbables(float radius)
    {
        // Get all colliders in radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);
        if (colliders.Length == 0)
            return null;

        // Apply weights to objects using the distance and player mouse direction
        float bestWeight = float.MaxValue;
        GameObject closestObject = null;

        foreach (Collider collider in colliders)
        {
            if (collider.gameObject.tag != "Grabbable")
                continue;

            // If has AIBehaviour, check if it's idle
            AIBehavior aiBehaviour = collider.gameObject.GetComponent<AIBehavior>();
            if (aiBehaviour != null && aiBehaviour.State != AIBehaviorState.Idle)
                continue;

            GameObject obj = collider.gameObject;

            float distance = Vector3.Distance(transform.position, obj.transform.position);
            float mouseDirection = Vector3.Dot(_locomotion.MouseVector, (obj.transform.position - transform.position).normalized);

            float weight = distance * ContainerDistanceWeight - mouseDirection * MouseDirectionWeight;

            if (weight < bestWeight)
            {
                bestWeight = weight;
                closestObject = obj;
            }
        }

        return GetHighestInCell(closestObject);
    }

    GameObject GetHighestInCell(GameObject obj)
    {
        if (obj == null)
            return null;

        float ceilingDistance = 3f;
        Collider[] aboveColliders = Physics.OverlapBox(obj.transform.position + Vector3.up * ceilingDistance / 2f, new Vector3(0.2f, ceilingDistance / 2f - 0.1f, 0.2f));

        if (aboveColliders.Length == 0)
            return obj;

        GameObject highestObject = obj;
        foreach (Collider collider in aboveColliders)
        {
            if (collider.gameObject.tag != "Grabbable")
                continue;

            if (highestObject == null || collider.transform.position.y > highestObject.transform.position.y)
                highestObject = collider.gameObject;
        }

        return highestObject;
    }

    void MoveObject()
    {
        // Get last direction from PlayerLocomotion script
        Vector3 direction = _locomotion.Direction;

        // Calculate goal position and distance to it
        Vector3 goalPosition = transform.position + direction + Vector3.up * FloatAmount;
        Vector3 goalDir = goalPosition - GrabbedObject.transform.position;
        float distance = Vector3.Distance(goalPosition, GrabbedObject.transform.position);

        // Add acceleration towards goal position, but only if the object is not already there, decrease speed while getting closer
        GrabbedObject.GetComponent<Rigidbody>().AddForce(goalDir * Acceleration * (distance * 0.5f) * Time.fixedDeltaTime, ForceMode.Acceleration); // TEST: GetComponent performance hit?

        // Apply negative force to keep the object in place
        GrabbedObject.GetComponent<Rigidbody>().AddForce(-GrabbedObject.GetComponent<Rigidbody>().velocity * DecelerationMultiplier, ForceMode.Acceleration);
    }

    void DebugVisuals()
    {
        Gizmos.DrawWireSphere(transform.position, AutoGrabRadius);
    }

    public ContainerGeneric GetActiveContainer()
    {
        if (GrabbedObject == null) return null;

        var container = GrabbedObject.GetComponent<ContainerGeneric>();
        return container == null ? null : container;
    }

    public bool GrabbableHasGridEffect()
    {
        if (GrabbedObject == null) return false;

        var container = GrabbedObject.GetComponent<ContainerGeneric>();
        return container == null ? false : container.HasGridEffect;
    }
}
