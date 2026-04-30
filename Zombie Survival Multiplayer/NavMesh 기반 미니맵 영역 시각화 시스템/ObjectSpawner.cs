using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ObjectSpawner : MonoBehaviourPun
{
    [SerializeField] Material iconMaterial;

    protected virtual void Update()
    {
        if (!GameManager.instance.isInGame)
            return;
    }

    protected GameObject SetMiniMapIcon(Transform transform)
    {
        GameObject iconPrefab = Resources.Load<GameObject>("MiniMapIcon");

        GameObject iconObj = Instantiate(iconPrefab, transform);
        iconObj.transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);

        if(iconObj.GetComponent<MeshRenderer>() is MeshRenderer mr)
            mr.material = iconMaterial;

        return iconObj;
    }

    protected IEnumerator DestroyAfter(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj != null)
        {
            PhotonNetwork.Destroy(obj);
        }
    }
}
