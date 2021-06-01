using UnityEngine;

public class NotUsable : MonoBehaviour
{
    public void Init(float width, float height)
    {
        Rect rect = GetComponent<RectTransform>().rect;
        transform.localScale = new Vector3(width / rect.width * .8f, height / rect.height * .8f, 1f);
    }

}
