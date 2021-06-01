using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Piece : MonoBehaviour
{
    [SerializeField] private Sprite[] _sprites;
    [SerializeField] private GameObject _selection;

    private int _type;
    private bool _notMoving = true;

    public UnityEvent StartMovingEvent;
    public UnityEvent StopMovingEvent;

    public int GetRandomType()
    {
        return Random.Range(0, _sprites.Length);
    }

    public void Init(int type, float width, float height)
    {
        if (type>=0 && type < _sprites.Length)
        {
            GetComponent<SpriteRenderer>().sprite = _sprites[type];
            this._type = type;
        }
        Rect rect = GetComponent<RectTransform>().rect;
        transform.localScale = new Vector3(width/ rect.width * .8f, height/ rect.height * .8f, 1f);
        
    }

    public int GetPieceType()
    {
        return _type;
    }

    public void Select()
    {
        _selection.gameObject.SetActive(true);
    }

    public void Deselect()
    {
        _selection.gameObject.SetActive(false);
    }

    public bool IsNotMoving()
    {
        return _notMoving;
    }

    public void Move(Vector3 toPos)
    {
        _notMoving = false;
        StartMovingEvent.Invoke();
        StartCoroutine(MoveToPosition(toPos));
    }

    IEnumerator MoveToPosition(Vector3 toPos)
    {
        Vector3 delta = (toPos - transform.position) / 20;
        for (int i = 0; i < 20; i++)
        {
            transform.position += delta;
            yield return new WaitForSeconds(.05f);
        }
        transform.position = toPos;
        StopMovingEvent.Invoke();
        _notMoving = true;
    }

    public void Remove()
    {
        StartCoroutine(MakeSmallerAndDestroy());
    }

    IEnumerator MakeSmallerAndDestroy()
    {
        for (int i = 0; i < 10; i++)
        {
            transform.localScale *= .8f;
            yield return new WaitForSeconds(.05f);
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        StartMovingEvent.RemoveAllListeners();
        StopMovingEvent.RemoveAllListeners();
    }
}
