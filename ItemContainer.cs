using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

public class ItemContainer : VisualElement
{
    public List<StoredItem> StoredItems = new List<StoredItem>();
    public static Dimensions SlotDimension { get; private set; }
    public Dimensions containerDimensions = new Dimensions();

    public ItemDefinition itemData;

    private VisualElement HeaderVisual;
    private Label HeaderLabelVisual;
    private VisualElement bodyVisual;
    private VisualElement gridVisual;

    private bool isDragging;
    private bool isInventoryReady;

    public ItemContainer(ItemDefinition itemDataDefinition)
    {
        itemData = itemDataDefinition;

        containerDimensions.Height = itemData.containerDimensions.Height;
        containerDimensions.Width = itemData.containerDimensions.Width;

        int dimensionY = itemData.containerDimensions.Height;
        int dimensionX = itemData.containerDimensions.Width;
        
        int gridHeight = dimensionY * 75;
        int gridWidth = dimensionX * 75;

        int labelBoxSize = 75;
        int offSet = 75;
        
        int canvasHeight = gridHeight + labelBoxSize + offSet;
        int canvasWidth = gridWidth + offSet;

        style.height = canvasHeight;
        style.width = canvasWidth;
        AddToClassList("itemContainerMain");

        HeaderVisual = new VisualElement();
        HeaderVisual.AddToClassList("itemContainerHeader");

        HeaderLabelVisual = new Label();
        HeaderVisual.Add(HeaderLabelVisual);
        HeaderLabelVisual.AddToClassList("itemContainerHeaderLabel");
        HeaderLabelVisual.text = itemData.FriendlyName;

        bodyVisual = new VisualElement();
        bodyVisual.AddToClassList("itemContainerBody");
        
        Add(HeaderVisual);
        Add(bodyVisual);

        gridVisual = new VisualElement();
        bodyVisual.Add(gridVisual);
        gridVisual.AddToClassList("itemContainerGrid");

        for (int y = 0; y < dimensionY; y++)
        {
            for (int x = 0; x < dimensionX; x++)
            {
                VisualElement slot = new VisualElement();
                gridVisual.Add(slot);
                slot.AddToClassList("itemContainerSlot");
            }
        }
        
        HeaderVisual.RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
        HeaderVisual.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
        HeaderVisual.RegisterCallback<MouseUpEvent>(OnMouseUpEvent);
        
        ConfigureSlotDimensions();

        isInventoryReady = true;
    }
    
    ~ItemContainer()
    {
        HeaderVisual.UnregisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
        HeaderVisual.UnregisterCallback<MouseDownEvent>(OnMouseDownEvent);
        HeaderVisual.UnregisterCallback<MouseUpEvent>(OnMouseUpEvent);
    }
    
    private void OnMouseDownEvent(MouseDownEvent mouseEvent)
    {
        isDragging = true;
    }

    private void OnMouseUpEvent(MouseUpEvent mouseEvent)
    {
        isDragging = false;
    }

    private void OnMouseMoveEvent(MouseMoveEvent mouseEvent)
    {
        if (!isDragging)
            return;
        
        Vector2 mousePos = mouseEvent.mousePosition;
        SetPosition(GetMousePosition(mousePos, HeaderVisual));
    }
    
    public Vector2 GetMousePosition(Vector2 mousePosition, VisualElement visualElement)
    {
        return new Vector2(
            mousePosition.x - (visualElement.layout.width / 2) - parent.worldBound.position.x, 
            mousePosition.y - (visualElement.layout.height / 2) - parent.worldBound.position.y);    
    }
    
    public void SetPosition(Vector2 pos)
    {
        style.left = pos.x;
        style.top = pos.y;
    }

    public void AddItem(ItemVisual item)
    {
        StoredItem toStore = new StoredItem(item.itemData, item);
        StoredItems.Add(toStore);
    }
    
    public async Task<bool> LoadInventory()
    {
        Debug.Log("times");
        await UniTask.WaitUntil(() => isInventoryReady);
        
        foreach (StoredItem loadedItem in StoredItems)
        {
            if (gridVisual.Contains(loadedItem.RootVisual))
                continue;
            
            ItemVisual inventoryItemVisual = new ItemVisual(loadedItem.Details);
            AddItemToInventoryGrid(inventoryItemVisual);
            
            bool inventoryHasSpace = await GetPositionForItem(inventoryItemVisual);

            if (!inventoryHasSpace)
            {
                Debug.Log("No space - Cannon pick up the item");
                RemoveItemFromInventoryGrid(inventoryItemVisual);
                return false;
            }
        
            ConfigureInventoryItem(loadedItem, inventoryItemVisual);
        }
        return true;
    }

    /*public async Task<bool> AddIfSpaceAvailable(ItemVisual loadedItem)
    {
        StoredItem itemToStore = new StoredItem(loadedItem.itemData, loadedItem);
        
        LoadInventory();
        
        ItemVisual inventoryItemVisual = new ItemVisual(itemToStore.Details);
        AddItemToInventoryGrid(inventoryItemVisual);
            
        bool inventoryHasSpace = await GetPositionForItem(inventoryItemVisual);

        if (!inventoryHasSpace)
        {
            Debug.Log("No space - Cannon pick up the item");
            RemoveItemFromInventoryGrid(inventoryItemVisual);
            return false;
        }
        
        StoredItems.Add(itemToStore);
        ConfigureInventoryItem(itemToStore, inventoryItemVisual);
        return true;
    }*/
    
    private static void ConfigureInventoryItem(StoredItem item, ItemVisual visual)
    {
        item.RootVisual = visual;
        visual.style.visibility = Visibility.Visible;
    }
    
    private async Task<bool> GetPositionForItem(VisualElement newItem)
    {
        for (int y = 0; y < containerDimensions.Height; y++)
        {
            for (int x = 0; x < containerDimensions.Width; x++)
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
        element.style.top = vector.y; ;
    }
    
    private void ConfigureSlotDimensions()
    {
        VisualElement firstSlot = gridVisual.Children().First();
        
        SlotDimension = new Dimensions
        {
            //todo check why this is messing up
            //Width = Mathf.RoundToInt(firstSlot.worldBound.width),
            //Height = Mathf.RoundToInt(firstSlot.worldBound.height)
            
            Width = 75,
            Height = 75
        };
    }
    
    private void AddItemToInventoryGrid(VisualElement item) => gridVisual.Add(item);
    private void RemoveItemFromInventoryGrid(VisualElement item) => gridVisual.Remove(item);

    /*#region UXML
    [Preserve]
    public new class UxmlFactory : UxmlFactory<ItemContainer, UxmlTraits> { }
    [Preserve]
    public new class UxmlTraits : VisualElement.UxmlTraits { }
    #endregion*/
}
