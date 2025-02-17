using System;

using UnityEngine;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ContainerGeneric : MonoBehaviour, IDamageable
{
    int _health = 12;
    public int Health
    {
        get => _health;
        private set => _health = value;
    }

    public ContainerType Type = ContainerType.Material;
    public ContainerRarity Rarity = ContainerRarity.Common;

    [Header("Grid Snap - Air Movement Settings")]

    [Range(0f, 1000f)]
    public float Acceleration = 150f;

    [Range(0f, 10f)]
    public float DecelerationMultiplier = 3.5f;

    [Tooltip("Maximum velocity of the player when grabbing an object")]
    public int MaxPlayerVelocity = 5;

    public GameObject DecalPrefab = null;

    [HideInInspector]
    public TileGridCell ParentCell = null; // Realitme parent cell - can be null

    [HideInInspector]
    public TileGridCell CurrentCell = null; // This will always be set to an object while the object is targeting a cell

    [HideInInspector]
    public bool HasGridEffect = false;

    float _lastHadGridEffect = 0f;

    bool _disableEffect = false;

    float COYOTE_TIME = 0.3f;

    Rigidbody _rb;
    TileGrid _tileGrid;
    PlayerGrab _playerGrab;
    GameObject _decalObject = null;
    DecalProjector _decalProjector = null;

    [HideInInspector]
    public HitEffect HitEffect = null;

    bool _renderDecal = false;
    float _lastRenderedDecal = 0f;

    int _maxHealth = -1;
    public int MaxHealth => _maxHealth;

    public bool IsGrabbed
    {
        get => _playerGrab != null && _playerGrab.GrabbedObject == gameObject;
    }

    bool _initialized = false;

    void Initialize()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            Debug.LogError("Rigidbody not found on object");

        if (DecalPrefab == null)
            DecalPrefab = Resources.Load("Prefabs/CrossDecal") as GameObject;

        if (DecalPrefab == null)
            Debug.LogError("Decal prefab not found (Prefabs/CrossDecal)");

        if (GetComponentInChildren<DecalProjector>() != null)
            _decalObject = GetComponentInChildren<DecalProjector>().gameObject;
        else
            _decalObject = Instantiate(DecalPrefab, transform);

        HitEffect = new HitEffect(gameObject);
        HitEffect.Initialize();

        // :3
        if ((int)Type < 4)
        {
            SetType(Type);
            SetRarity(Rarity);
        }

        _decalProjector = _decalObject.GetComponentInChildren<DecalProjector>();
        _tileGrid = TileGrid.FindTileGrid();

        _maxHealth = Health;

        _initialized = true;
    }

    void Update()
    {
        if (!_initialized)
        {
            Initialize();
            return;
        }

        _playerGrab = transform.parent == null ? null : transform.parent.GetComponent<PlayerGrab>();

        PushTowardsParentCell();
        ProcessCoyoteTime();
        CorrectRotation();
        UpdateDecal();
        KillIfOutOfBounds();

        HitEffect.Update();
    }

    void KillIfOutOfBounds()
    {
        if (transform.position.y <= -50)
        {
            Destroy(gameObject);
        }
    }

    void ProcessCoyoteTime()
    {
        if (!HasGridEffect && ParentCell != null)
        {
            HasGridEffect = true;
            CurrentCell = ParentCell;
        }

        // Reset current cell if it's been changed externally
        if (ParentCell != null && CurrentCell != ParentCell)
        {
            CurrentCell = ParentCell;
            _disableEffect = false;
            _lastHadGridEffect = 0f;
        }

        // Disable grid effect if the object has been in the air for a while
        if (_disableEffect && _lastHadGridEffect + COYOTE_TIME < Time.time && ParentCell == null)
        {
            HasGridEffect = false;
            CurrentCell = null;
            _disableEffect = false;
        }

        if (HasGridEffect && ParentCell == null)
            _disableEffect = true;

        if (ParentCell != null)
            _lastHadGridEffect = Time.time;
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        var textPosition = transform.position + new Vector3(0, 0.5f, 0);

        if (ParentCell != null)
        {
            Handles.Label(textPosition, "Parent: " + ParentCell.Tile.GridPosition);
            textPosition += new Vector3(0, 0.2f, 0);
        }
#endif      
    }

    Vector3 Vector2D(Vector3 vector)
    {
        return new Vector3(vector.x, 0, vector.z);
    }

    void PushTowardsParentCell()
    {
        if (ParentCell == null)
            return;

        // If has AI agent, don't apply forces
        if (gameObject.GetComponent<AIWander>() != null && !IsGrabbed)
            return;

        // Don't apply forces if the player is running faster than the max velocity
        if (transform.parent)
        {
            var locomotion = transform.parent.GetComponent<PlayerLocomotion>();

            if (locomotion != null && locomotion.Velocity.magnitude > MaxPlayerVelocity)
                return;
        }

        Vector3 direction = Vector2D((ParentCell.transform.position - new Vector3(0, _tileGrid.CellSize.x / 2, 0)) - transform.position).normalized;

        // Distance to parent cell ignoring y axis
        float distance = Vector3.Distance(Vector2D(transform.position), Vector2D(ParentCell.transform.position));

        // Limit the force so it is always less than gravity
        Vector3 force = direction * Acceleration * Time.deltaTime;

        if (force.magnitude > Physics.gravity.magnitude)
            force = force.normalized * Physics.gravity.magnitude;

        if (distance > 0.1f)
        {
            GetComponent<Rigidbody>().AddForce(force);
        }
        else
        {
            // Apply negative force to keep the object in place
            var rigidbody = GetComponent<Rigidbody>();
            rigidbody.AddForce(-Vector2D(rigidbody.velocity) * DecelerationMultiplier, ForceMode.Acceleration);
        }
    }

    void CorrectRotation()
    {
        if (ParentCell == null)
            return;

        // If has AI agent, don't apply forces
        if (gameObject.GetComponent<AIWander>() != null && !IsGrabbed)
            return;

        if (_rb.velocity.magnitude < 0.1f)
            return;

        if (_playerGrab != null && _playerGrab.GrabbedObject == gameObject)
            return;

        // Round to nearest 90 degrees considering all 3 axis
        Vector3 idealAngle = transform.rotation.eulerAngles; // i couldnt find any function to do this in one line :(
        idealAngle.x = Mathf.Round(idealAngle.x / 90) * 90;
        idealAngle.y = Mathf.Round(idealAngle.y / 90) * 90;
        idealAngle.z = Mathf.Round(idealAngle.z / 90) * 90;

        Quaternion targetRotation = Quaternion.Euler(idealAngle.x, idealAngle.y, idealAngle.z);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    void UpdateDecal()
    {
        if (_decalProjector == null) return;

        _decalObject.transform.position = Vector3.Lerp(_decalObject.transform.position, transform.position + Vector3.down * 0.3f, Time.deltaTime * 5f);
        _decalObject.transform.rotation = Quaternion.Euler(0, 0, 0);

        if (_renderDecal) _decalProjector.enabled = true;
        else _decalProjector.enabled = false;

        if (_lastRenderedDecal + 0.1f < Time.time)
        {
            _renderDecal = false;
        }
    }

    public void RenderDecal()
    {
        if (_decalProjector == null)
            return;

        _renderDecal = true;
        _lastRenderedDecal = Time.time;
    }

    public void Damage(int damage)
    {
        Health = Mathf.Clamp(Health - damage, 0, _maxHealth);
        HitEffect.OnDamage();

        // If does not have AI Wander, destroy the object
        if (gameObject.GetComponent<AIBehavior>() == null && Health <= 0)
        {
            Destroy(gameObject);
        }
    }
    public void Heal(int heal) => Health = Mathf.Clamp(Health + heal, 0, _maxHealth);

    public void SetType(ContainerType type)
    {
        Type = type;

        // Show the correct rarity in the child named according to the rarities
        var container = transform.Find(type.ToString());
        if (container == null)
        {
            Debug.LogWarning("Container " + name + " does not have a child named " + type.ToString());
        }
        SetRenderers(container.gameObject, true);

        foreach (var t in Enum.GetValues(typeof(ContainerType)))
        {
            if ((ContainerType)t != Type)
            {
                var child = transform.Find(t.ToString());
                if (child != null)
                    SetRenderers(child.gameObject, false);
            }
        }
    }

    public void SetRarity(ContainerRarity rarity)
    {
        Rarity = rarity;

        // Set the material to the correct rarity
        SetRarityMaterial();
    }

    void SetRarityMaterial()
    {
        // Get the material of the child named "MODULATE"
        var material = transform.Find(Type.ToString()).Find("MODULATE").GetComponent<Renderer>().material;

        // Set the material to the correct rarity
        switch (Rarity)
        {
            case ContainerRarity.Common:
                material.SetColor("_BaseColor", new Color(38 / 255f, 46 / 255f, 86 / 255f)); // Def
                break;
            case ContainerRarity.Rare:
                material.SetColor("_BaseColor", new Color(157 / 255f, 157 / 255f, 157 / 255f)); // Silver
                break;
            case ContainerRarity.Epic:
                material.SetColor("_BaseColor", new Color(219 / 255f, 176 / 255f, 51 / 255f)); // Gold
                break;
            case ContainerRarity.Legendary:
                material.SetColor("_BaseColor", new Color(219 / 255f, 176 / 255f, 51 / 255f)); // Gold

                // Set the other children materials to material as well
                foreach (Transform child in transform.Find(Type.ToString()))
                {
                    if (child.name == "MODULATE") continue;
                    child.GetComponent<Renderer>().material = material;
                }
                break;
        }
    }

    void SetRenderers(GameObject obj, bool enabled)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = enabled;
        }
    }
}
