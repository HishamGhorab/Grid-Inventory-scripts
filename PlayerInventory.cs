using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance;
    
    private VisualElement m_Root;
    private VisualElement m_InventoryGrid;
    private static Label m_ItemDetailHeader;
    private static Label m_ItemDetailBody;
    private static Label m_ItemDetailPrice;
    private bool m_IsInventoryReady;
    public static Dimensions SlotDimension { get; private set; }
    
    public List<StoredItem> StoredItems = new List<StoredItem>();
    public Dimensions InventoryDimensions;
    
    private VisualElement m_Telegraph;

    public bool itemIsBeingDragged;
    private ItemVisual draggedItem;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Configure();
        }
        else if (Instance != this)
        {
            Destroy(this);    
        }
    }

    private void Start()
    {
        LoadInventory();
    }

    private void Update()
    {
        if (itemIsBeingDragged)
        {
            if (Input.GetKeyDown(KeyCode.R) && draggedItem != null)
            {
                int newHeight;
                int newWidth;
                
                if (!draggedItem.isRotated90)
                {
                    newHeight = draggedItem.dimensions.Width;
                    newWidth = draggedItem.dimensions.Height;
                    draggedItem.UpdateSlotHeightWidth(newHeight, newWidth);
                    
                    draggedItem.icon.style.rotate = new Rotate(90);
                    draggedItem.isRotated90 = true;
                }
                else
                {
                    newHeight = draggedItem.dimensions.Width;
                    newWidth = draggedItem.dimensions.Height;
                    draggedItem.UpdateSlotHeightWidth(newHeight, newWidth);
                    
                    draggedItem.icon.style.rotate = new Rotate(0);
                    draggedItem.isRotated90 = false;
                }
            }
        }
    }

    private async void Configure()
    {
        m_Root = GetComponent<UIDocument>().rootVisualElement;
        m_InventoryGrid = m_Root.Q<VisualElement>("Grid");
        VisualElement itemDetails = m_Root.Q<VisualElement>("ItemDetails");
        
        m_ItemDetailHeader  = m_Root.Q<Label>("Header");
        m_ItemDetailBody = m_Root.Q<Label>("Body");
        m_ItemDetailPrice = m_Root.Q<Label>("SellPrice");
        
        ConfigureInventoryTelegraph();

        await UniTask.WaitForEndOfFrame();

        ConfigureSlotDimensions();

        m_IsInventoryReady = true;
    }

    private void ConfigureSlotDimensions()
    {
        VisualElement firstSlot = m_InventoryGrid.Children().First();
        
        SlotDimension = new Dimensions
        {
            Width = Mathf.RoundToInt(firstSlot.worldBound.width),
            Height = Mathf.RoundToInt(firstSlot.worldBound.height)
        };
    }
    
    private async void LoadInventory()
    {
        await UniTask.WaitUntil(() => m_IsInventoryReady);
        
        foreach (StoredItem loadedItem in StoredItems)
        {
            ItemVisual inventoryItemVisual = new ItemVisual(loadedItem.Details);
            AddItemToInventoryGrid(inventoryItemVisual);
            
            bool inventoryHasSpace = await GetPositionForItem(inventoryItemVisual);

            if (!inventoryHasSpace)
            {
                Debug.Log("No space - Cannon pick up the item");
                RemoveItemFromInventoryGrid(inventoryItemVisual);
                continue;
            }

            ConfigureInventoryItem(loadedItem, inventoryItemVisual);
        }
    }
    
    private void AddItemToInventoryGrid(VisualElement item) => m_InventoryGrid.Add(item);
    private void RemoveItemFromInventoryGrid(VisualElement item) => m_InventoryGrid.Remove(item);

    public void RemoveItem(ItemVisual item)
    {
        RemoveItemFromInventoryGrid(item);
        
        StoredItem s = new StoredItem(item.itemData, item);
        StoredItems.Remove(s);
    }
    
    private static void ConfigureInventoryItem(StoredItem item, ItemVisual visual)
    {
        item.RootVisual = visual;
        visual.style.visibility = Visibility.Visible;
    }

    private async Task<bool> GetPositionForItem(VisualElement newItem)
    {
        for (int y = 0; y < InventoryDimensions.Height; y++)
        {
            for (int x = 0; x < InventoryDimensions.Width; x++)
            {
                SetItemPosition(newItem, new Vector2(SlotDimension.Width * x, SlotDimension.Height * y));

                await UniTask.WaitForEndOfFrame();

                StoredItem overlappingItem = StoredItems.FirstOrDefault
                (
                    s => s.RootVisual != null && s.RootVisual.layout.Overlaps(newItem.layout) 
                );

                if (overlappingItem == null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void SetItemPosition(VisualElement element, Vector2 vector)
    {
        element.style.left = vector.x;
        element.style.top = vector.y;
    }

    private void ConfigureInventoryTelegraph()
    {
        m_Telegraph = new VisualElement
        {
            name = "Telegraph",
            style =
            {
                position = Position.Absolute,
                visibility = Visibility.Hidden
            }
        };
        
        m_Telegraph.AddToClassList("slot-icon-highlighted");
        AddItemToInventoryGrid(m_Telegraph);
    }

    public (bool canPlace, bool overlapContainer, Vector2 position, StoredItem container) ShowPlacementTarget(ItemVisual _draggedItem)
    {
        draggedItem = _draggedItem;

        //Checks to see whether the dragged item is hanging over the edge and if so,
        //skips drawing the telegraph and instead returns false and Vector2.zero.
        if (!m_InventoryGrid.layout.Contains(new Vector2(draggedItem.localBound.xMax,
                draggedItem.localBound.yMax)))
        {
            m_Telegraph.style.visibility = Visibility.Hidden;
            return (canPlace: false, overlapContainer: false, position: Vector2.zero, null);
        }
        
        //Finds the closest inventory grid slot relative to the dragged item by checking
        //for all overlapping elements and sorting by distance.
        VisualElement targetSlot = m_InventoryGrid.Children().Where(x => 
            x.layout.Overlaps(draggedItem.layout) && x != draggedItem).OrderBy(x => 
            Vector2.Distance(x.worldBound.position, draggedItem.worldBound.position)).First();
        
        //Set the width and height of the telegraph based on the dragged item
        m_Telegraph.style.width = draggedItem.style.width;
        m_Telegraph.style.height = draggedItem.style.height;
        
        //Debug.Log($"Height {m_Telegraph.style.height} Width {m_Telegraph.style.width}");
        
        //Set the position of the telegraph and change toggles the visibility to on.
        SetItemPosition(m_Telegraph, new Vector2(targetSlot.layout.position.x, 
            targetSlot.layout.position.y));

        m_Telegraph.style.visibility = Visibility.Visible;
        
        //Check whether the target location of the dragged item is overlapping any other ItemVisuals,
        //and if so returns false and Vector2.zero. 
        var overlappingItems = StoredItems.Where(x => x.RootVisual != null && 
            x.RootVisual.layout.Overlaps(m_Telegraph.layout)).ToArray();
        
        if (overlappingItems.Length > 1)
        {
            if (overlappingItems[0].Details.isContainer)
            {
                Debug.Log(overlappingItems[0].RootVisual.Container.StoredItems.Count);

                m_Telegraph.style.visibility = Visibility.Hidden;
                return (canPlace: false, overlapContainer: true, position: Vector2.zero, overlappingItems[0]);
            }
            
            m_Telegraph.style.visibility = Visibility.Hidden;
            return (canPlace: false, overlapContainer: false, position: Vector2.zero, null);
        }
        
        //All checks have passed, so the method returns true and the target position.
        return (canPlace: true, overlapContainer: false, targetSlot.worldBound.position, null);
    }
    
    public void GetContainer(ItemVisual itemVisual)
    {
        ItemContainer container = itemVisual.Container;
        m_Root.Add(container);

        //container.LoadInventory();
    }

    public void DeleteContainer(ItemVisual itemVisual)
    {
        m_Root.Remove(itemVisual.Container);
    }
}

[Serializable]
public class StoredItem
{
    public StoredItem(ItemDefinition Details, ItemVisual RootVisual)
    {
        this.Details = Details;
        this.RootVisual = RootVisual;
    }
    
    public ItemDefinition Details;
    public ItemVisual RootVisual;
}
