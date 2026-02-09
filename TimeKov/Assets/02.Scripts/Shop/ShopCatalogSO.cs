using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Game Data/Shop Catalog")]
public class ShopCatalogSO : ScriptableObject
{
    public List<ShopEntry> entries = new List<ShopEntry>();
}

[Serializable]
public class ShopEntry
{
    public int itemId;
    public int buyPrice;
    public int stock = -1; // -1 = ¹«ÇÑ
}
