using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UI_FocusKeeper : MonoBehaviour
{
    [Header("포커스 대상 설정")]
    [SerializeField] private Selectable _defaultSelectable;
    [SerializeField] private bool _autoRestoreOnDeselect = true;

    [Header("모니터링")]
    [SerializeField, ReadOnly] private GameObject _lastSelected;

    public Selectable DefaultSelectable
    {
        get => _defaultSelectable;
        set => _defaultSelectable = value;
    }

    public GameObject LastSelected => _lastSelected;

    private void OnEnable()
    {
        InitializeFocus();
    }

    private void OnDisable()
    {
        ResetFocus();
    }

    private void LateUpdate()
    {
        UpdateFocus();
    }

    public void SetDefaultSelectable(Selectable selectable)
    {
        _defaultSelectable = selectable;
    }

    public void ResetFocus()
    {
        _lastSelected = null;
    }

    public void RestoreFocus()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject targetToSelect = null;

        if (HasValidTarget(_lastSelected))
        {
            targetToSelect = _lastSelected;
        }
        else if (_defaultSelectable != null && HasValidTarget(_defaultSelectable.gameObject))
        {
            targetToSelect = _defaultSelectable.gameObject;
        }

        if (targetToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(targetToSelect);
            _lastSelected = targetToSelect;
        }
    }

    private void InitializeFocus()
    {
        if (_defaultSelectable == null)
        {
            _defaultSelectable = GetComponentInChildren<Selectable>();
        }

        if (EventSystem.current == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (HasValidTarget(currentSelected))
        {
            _lastSelected = currentSelected;
            return;
        }

        if (_defaultSelectable != null && HasValidTarget(_defaultSelectable.gameObject))
        {
            EventSystem.current.SetSelectedGameObject(_defaultSelectable.gameObject);
            _lastSelected = _defaultSelectable.gameObject;
        }
    }

    private void UpdateFocus()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;

        if (HasValidTarget(currentSelected))
        {
            _lastSelected = currentSelected;
            return;
        }

        if (ShouldRestoreFocus())
        {
            RestoreFocus();
        }
    }

    private bool ShouldRestoreFocus()
    {
        if (!_autoRestoreOnDeselect)
        {
            return false;
        }

        return HasValidTarget(_lastSelected) || (_defaultSelectable != null && HasValidTarget(_defaultSelectable.gameObject));
    }

    private bool HasValidTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (!target.activeInHierarchy)
        {
            return false;
        }

        Selectable selectable = target.GetComponent<Selectable>();
        if (selectable != null && !selectable.interactable)
        {
            return false;
        }

        return true;
    }
}
