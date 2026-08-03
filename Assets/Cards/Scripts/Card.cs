using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cyan.Cards
{

    [System.Serializable]
    public class CardData
    {
        //[Tooltip("卡牌的唯一ID（你朋友靠这个判断放什么技能，比如 'move_forward', 'fireball'）")]
        //public string cardID = "attack_01";

        //[Tooltip("卡牌类型（如 Attack, Move, Defend 等）")]
        //public string cardType = "Attack";

        //[Tooltip("卡牌的数值（如造成5点伤害，或向前走3步）")]
        //public int value = 5;
    }

    public class Card : MonoBehaviour
    {

        [Tooltip("Mana required to use card")]
        public int mana;

        [Tooltip("【传给人物的卡牌数据】")]
        public CardData data; 

        protected Color color;
        protected Color color2;

        protected MeshRenderer meshRenderer;
        protected Material material;

        protected Vector2 dissolveOffset = new Vector2(0.1f, 0);
        protected Vector2 dissolveSpeed = new Vector2(2f, 2f);
        protected Color dissolveColor;

        protected bool isInactive;

        protected virtual void Start()
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            material = meshRenderer.material;

            color = material.GetColor("_Color");
            color2 = material.GetColor("_OutlineColor");
            dissolveColor = material.GetColor("_DissolveColor");
        }

        public virtual void Use()
        {
            StartCoroutine(Dissolve());
        }

        protected IEnumerator Dissolve()
        {
            Vector2 t = Vector2.zero - dissolveOffset;
            while (t.x < 1)
            {
                t.x = (t.x + Time.deltaTime * dissolveSpeed.x);
                if (t.y < 1)
                {
                    t.y = (t.y + Time.deltaTime * dissolveSpeed.y);
                }
                material.SetVector("_Dissolve", t);
                material.SetColor("_DissolveColor", dissolveColor * 4 * t.y);
                yield return null;
            }
        }

        protected virtual void OnDisable()
        {
            if (material != null)
            {
                material.SetVector("_Dissolve", Vector2.zero - dissolveOffset);
                material.SetColor("_DissolveColor", dissolveColor * 0);
            }
        }

        public virtual void SetInactiveMaterialState(bool isInactive, Material inactiveMaterial = null)
        {
            if (isInactive == this.isInactive) return;
            this.isInactive = isInactive;

            if (isInactive) meshRenderer.sharedMaterial = inactiveMaterial;
            else meshRenderer.sharedMaterial = material;
        }

        public virtual void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}