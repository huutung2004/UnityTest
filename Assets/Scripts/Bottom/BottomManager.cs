using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BottomManager : MonoBehaviour
{
    public event Action OnBottomFull = delegate { };
    public event Action OnBottomChanged = delegate { };

    [SerializeField] private Transform[] slotTransforms;

    private List<Item> slots = new List<Item>();

    public int Capacity => slotTransforms != null && slotTransforms.Length > 0 ? slotTransforms.Length : slots.Count;

    private void Awake()
    {
        if (slotTransforms != null && slotTransforms.Length > 0)
        {
            slots = new List<Item>(new Item[slotTransforms.Length]);
        }
    }

    public void Init(int capacity, Transform[] transforms = null)
    {
        if (transforms != null && transforms.Length == capacity)
        {
            slotTransforms = transforms;
        }
        else
        {
            slotTransforms = new Transform[capacity];
            for (int i = 0; i < capacity; i++)
            {
                GameObject go = new GameObject("BottomSlot_" + i);
                go.transform.SetParent(this.transform);
                go.transform.localPosition = new Vector3(i * 1.0f, 0f, 0f);
                slotTransforms[i] = go.transform;
            }
        }

        slots = new List<Item>(new Item[capacity]);
    }

    public bool TryAddItemToBottom(Item item, float moveDuration = 0.35f, Action onComplete = null)
    {
        int index = slots.FindIndex(s => s == null);
        if (index == -1)
        {
            OnBottomFull();
            return false;
        }

        slots[index] = item;

        if (item.View != null)
        {
            item.View.transform.SetParent(slotTransforms[index], true);
            item.View.transform.DOMove(slotTransforms[index].position, moveDuration).OnComplete(() =>
            {
                item.View.transform.localPosition = Vector3.zero;
                onComplete?.Invoke();

                CheckTripleForType(item);
                OnBottomChanged();
            });
        }
        else
        {
            onComplete?.Invoke();
            CheckTripleForType(item);
            OnBottomChanged();
        }

        return true;
    }

    private void CheckTripleForType(Item addedItem)
    {
        if (addedItem == null) return;

        string key = GetItemKey(addedItem);

        var indices = new List<int>();
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            if (s != null && GetItemKey(s) == key)
                indices.Add(i);
        }

        if (indices.Count == 3)
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int idx = indices[i];
                var it = slots[idx];
                if (it != null && it.View != null)
                {
                    it.View.transform.DOScale(Vector3.zero, 0.2f).OnComplete(() =>
                    {
                        if (it.View != null)
                            Destroy(it.View.gameObject);
                    });
                }
                slots[idx] = null;
            }

            CompressLeft();
            OnBottomChanged();
        }
    }

    private void CompressLeft()
    {
        int write = 0;
        for (int read = 0; read < slots.Count; read++)
        {
            if (slots[read] != null)
            {
                if (read != write)
                {
                    Item it = slots[read];
                    slots[write] = it;
                    slots[read] = null;

                    if (it.View != null)
                    {
                        it.View.transform.SetParent(slotTransforms[write], true);
                        it.View.transform.DOMove(slotTransforms[write].position, 0.2f).OnComplete(() =>
                        {
                            it.View.transform.localPosition = Vector3.zero;
                        });
                    }
                }
                write++;
            }
        }
    }

    private string GetItemKey(Item item)
    {
        if (item is NormalItem n)
        {
            return n.ItemType.ToString();
        }

        return item.GetType().Name;
    }

    public bool IsFull()
    {
        return slots.All(s => s != null);
    }

    public bool IsEmpty()
    {
        return slots.All(s => s == null);
    }

    public int CountFilled()
    {
        return slots.Count(s => s != null);
    }

    public void ClearAll()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var it = slots[i];
            if (it != null && it.View != null)
            {
                Destroy(it.View.gameObject);
            }
            slots[i] = null;
        }
        OnBottomChanged();
    }
    public bool GetNeededItemTypes(out List<string> neededTypes)
    {
        var counts = slots
            .Where(item => item != null)
            .GroupBy(item => GetItemKey(item))
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .ToList();

        neededTypes = counts
            .Where(c => c.Count < 3)
            .Select(c => c.Key)
            .ToList();

        return neededTypes.Count > 0;
    }
    public bool GetMostFrequentItemType(out string mostFrequentKey)
    {
        mostFrequentKey = null;

        var counts = slots
            .Where(item => item != null)
            .GroupBy(item => GetItemKey(item))
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .ToList();

        var frequentKeys = counts
            .Where(c => c.Count < 3)
            .OrderByDescending(c => c.Count)
            .ToList();

        if (frequentKeys.Count > 0)
        {
            mostFrequentKey = frequentKeys.First().Key;
            return true;
        }

        return false;
    }

    public Item GetItemByView(GameObject view)
    {
        return slots.FirstOrDefault(item => item != null && item.View == view);
    }
    public bool RemoveItem(Item item)
    {
        if (item == null) return false;

        // Tìm index của item trong slots
        int index = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == item)
            {
                index = i;
                break;
            }
        }

        if (index == -1) return false;

        // Xóa item khỏi slot
        slots[index] = null;

        // Dồn các item còn lại sang trái
        CompressLeft2();

        // Cập nhật giao diện
        OnBottomChanged();

        return true;
    }
    private void CompressLeft2()
    {
        // Tạo mảng tạm để lưu các item không null
        Item[] temp = new Item[slots.Count];
        int tempIndex = 0;

        // Di chuyển tất cả item không null sang trái
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                temp[tempIndex] = slots[i];
                tempIndex++;
            }
        }

        // Copy lại vào slots gốc
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i] = temp[i];
        }

        // Cập nhật vị trí visual cho các item
        UpdateItemsPosition();
    }
    private void UpdateItemsPosition()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].View != null)
            {
                Vector3 targetPos = GetPositionForIndex(i);
                slots[i].MoveToPosition(targetPos, 0.2f);
            }
        }
    }
    private Vector3 GetPositionForIndex(int index)
    {
        // Tính toán vị trí dựa trên index
        float spacing = 1.0f; // Khoảng cách giữa các slot
        float startX = -2f;   // Vị trí bắt đầu, điều chỉnh theo nhu cầu

        return new Vector3(startX + (index * spacing), 0f, 0f);
    }


}
