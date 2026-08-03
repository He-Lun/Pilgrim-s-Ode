using System;
using UnityEngine;

/// <summary>
/// 世界空间血条 — 跟随角色锚点并绑定 AttributeSet。
/// </summary>
public class WorldHealthBarWidget : MonoBehaviour
{
    [SerializeField] private HealthBarView healthBar;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    private Transform followTarget;
    private AttributeSet boundAttributes;
    private Action<float> healthChangedHandler;
    private Action<string, float, float> attributeChangedHandler;
    private Action<GameplayTag> modifierChangedHandler;
    private Action<AbilitySystemComponent> deathHandler;
    private AbilitySystemComponent boundActor;

    public AbilitySystemComponent BoundActor => boundActor;

    void Awake()
    {
        healthBar ??= GetComponentInChildren<HealthBarView>(true);
        healthBar?.AutoBindFromHierarchy();
        EnsureDepletionChips();

        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }
    }

    public void Bind(AbilitySystemComponent actor, Transform anchor)
    {
        Unbind();

        boundActor = actor;
        followTarget = anchor != null ? anchor : actor != null ? actor.transform : null;
        boundAttributes = actor != null ? actor.Attributes : null;

        if (followTarget != null)
        {
            var scale = transform.localScale;
            transform.SetParent(followTarget, false);
            transform.localPosition = worldOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = scale;
        }

        if (boundAttributes == null)
        {
            gameObject.SetActive(false);
            return;
        }

        healthChangedHandler = _ => Refresh();
        attributeChangedHandler = (name, _, __) =>
        {
            if (name == "Health")
                Refresh();
        };
        modifierChangedHandler = _ => SyncDisplay();

        boundAttributes.OnHealthChanged += healthChangedHandler;
        boundAttributes.OnAttributeChanged += attributeChangedHandler;
        boundAttributes.OnModifierAdded += modifierChangedHandler;
        boundAttributes.OnModifierRemoved += modifierChangedHandler;

        if (actor != null)
        {
            deathHandler = _ => SetVisible(false);
            actor.OnDeath += deathHandler;
        }

        SyncDisplay();
        SetVisible(!boundAttributes.IsDead());
    }

    public void SyncDisplay()
    {
        if (healthBar == null || boundAttributes == null)
            return;

        healthBar.SyncValues(boundAttributes.CurrentHealth, boundAttributes.MaxHealth);
    }

    public void Unbind()
    {
        if (boundAttributes != null && healthChangedHandler != null)
            boundAttributes.OnHealthChanged -= healthChangedHandler;

        if (boundAttributes != null && attributeChangedHandler != null)
            boundAttributes.OnAttributeChanged -= attributeChangedHandler;

        if (boundAttributes != null && modifierChangedHandler != null)
        {
            boundAttributes.OnModifierAdded -= modifierChangedHandler;
            boundAttributes.OnModifierRemoved -= modifierChangedHandler;
        }

        if (boundActor != null && deathHandler != null)
            boundActor.OnDeath -= deathHandler;

        boundActor = null;
        boundAttributes = null;
        followTarget = null;
        healthChangedHandler = null;
        attributeChangedHandler = null;
        modifierChangedHandler = null;
        deathHandler = null;
    }

    void LateUpdate()
    {
        if (followTarget == null || transform.parent == followTarget)
            return;

        transform.position = followTarget.position + worldOffset;
    }

    void OnDestroy()
    {
        Unbind();
    }

    public void Refresh()
    {
        if (healthBar == null || boundAttributes == null)
            return;

        healthBar.SetValues(boundAttributes.CurrentHealth, boundAttributes.MaxHealth);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            return;
        }

        gameObject.SetActive(visible);
    }

    private void EnsureDepletionChips()
    {
        if (healthBar == null)
            return;

        var chips = healthBar.GetComponent<HealthBarDepletionChips>();
        if (chips == null)
            chips = healthBar.gameObject.AddComponent<HealthBarDepletionChips>();
        chips.Bind(healthBar);
    }
}
