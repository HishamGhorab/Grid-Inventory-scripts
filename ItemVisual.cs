using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;

public class ItemVisual : VisualElement
{
    public readonly ItemDefinition itemData;
    public ItemContainer Container;

    public Dimensions dimensions = new Dimensions();

    private bool m_IsDragging;
    public bool isRotated90;
    private bool containerIsOpen;
    private (bool canPlace, bool overlapContainer, Vector2 position, StoredItem container) m_PlacementResults;

    private Vector2 mousePos;
    private Vector2 m_OriginalPosition;
    
    public VisualElement icon;

    public ItemVisual(ItemDefinition itemData)
    {        
        this.itemData = itemData;

        dimensions.Height = this.itemData.SlotDimensions.Height;
        dimensions.Width = this.itemData.SlotDimensions.Width;

        name = $"{this.itemData.FriendlyName}";
        name = $"{this.itemData.FriendlyName}";
        UpdateSlotHeightWidth(dimensions.Height, dimensions.Width);
        
        style.visibility = Visibility.Hidden;

        icon = new VisualElement
        {
            style = {backgroundImage = this.itemData.Icon.texture}
        };
        Add(icon);
        
        icon.AddToClassList("visual-icon");
        AddToClassList("visual-icon-container");
        
        RegisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
        RegisterCallback<MouseUpEvent>(OnMouseUpEvent);

        if (itemData.isContainer)
        {
            Container = new ItemContainer(itemData);
        }
    }
    
    ~ItemVisual()
    {
        UnregisterCallback<MouseMoveEvent>(OnMouseMoveEvent);
        UnregisterCallback<MouseUpEvent>(OnMouseUpEvent);
    }
    
    public void UpdateSlotHeightWidth(int height, int width)
    {
        style.height = height * PlayerInventory.SlotDimension.Height;
        style.width = width * PlayerInventory.SlotDimension.Width;

        dimensions.Height = height;
        dimensions.Width = width;
    }
    
    private async void OnMouseUpEvent(MouseUpEvent mouseEvent)
    {
        if (mouseEvent.button == 0)
        {
            if (!m_IsDragging)
            {
                StartDrag();
                return;
            }

            m_IsDragging = false;

            if (m_PlacementResults.canPlace)
            {
                PlayerInventory.Instance.itemIsBeingDragged = false;

                //what happens here exactly?
                SetPosition(new Vector2(
                    m_PlacementResults.position.x - parent.worldBound.position.x,
                    m_PlacementResults.position.y - parent.worldBound.position.y));
                return;
            }
            
            if (m_PlacementResults.overlapContainer)
            {
                ItemContainer container = m_PlacementResults.container.RootVisual.Container;
                
                container.AddItem(this);
                
                bool available = await container.LoadInventory();
                
                if (available)
                {
                    PlayerInventory.Instance.RemoveItem(this);
                    return;
                }
            }

            SetPosition(new Vector2(m_OriginalPosition.x, m_OriginalPosition.y));   
        }
        else
        {
            if (itemData.isContainer && !containerIsOpen)
            {
                PlayerInventory.Instance.GetContainer(this);
                containerIsOpen = true;
            }
            else if(itemData.isContainer && containerIsOpen)
            {
                PlayerInventory.Instance.DeleteContainer(this);
                containerIsOpen = false;
            }
        }
    }

    private void StartDrag()
    {
        PlayerInventory.Instance.itemIsBeingDragged = true;
        
        m_IsDragging = true;

        m_OriginalPosition = worldBound.position - parent.worldBound.position;
        BringToFront();
    }

    private void OnMouseMoveEvent(MouseMoveEvent mouseEvent)
    {
        if (!m_IsDragging)
            return;

        mousePos = mouseEvent.mousePosition;
        SetPosition(GetMousePosition(mousePos));
        m_PlacementResults = PlayerInventory.Instance.ShowPlacementTarget(this);
    }

    public Vector2 GetMousePosition(Vector2 mousePosition)
    {
        return new Vector2(
            mousePosition.x - (layout.width / 2) - parent.worldBound.position.x, 
            mousePosition.y - (layout.height / 2) - parent.worldBound.position.y);    
    }

    public void SetPosition(Vector2 pos)
    {
        style.left = pos.x;
        style.top = pos.y;
    }
}
