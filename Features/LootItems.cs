﻿using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Trainer.Configuration;
using EFT.Trainer.Extensions;
using JsonType;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features
{
	internal class LootItems : PointOfInterests
	{
		public override string Name => "loot";

		[ConfigurationProperty]
		public Color Color { get; set; } = Color.cyan;

		[ConfigurationProperty(Browsable = false, Comment = @"Example: [""foo"", ""bar""] or with extended properties: [{""Name"":""foo"",""Color"":[1.0,0.0,0.0,1.0]},{""Name"":""bar"",""Color"":[1.0,1.0,1.0,0.8],""Rarity"":""Rare""}]")]
		public List<TrackedItem> TrackedNames { get; set; } = new();

		[ConfigurationProperty] 
		public bool SearchInsideContainers { get; set; } = true;

		[ConfigurationProperty]
		public bool SearchInsideCorpses { get; set; } = true;

		[ConfigurationProperty]
		public bool ShowPrices { get; set; } = true;

		public override float CacheTimeInSec { get; set; } = 3f;
		public override Color GroupingColor => Color;

		public bool Track(string lootname, Color? color, ELootRarity? rarity)
		{
			lootname = lootname.Trim();

			if (TrackedNames.Any(t => t.Name == lootname))
				return false;

			TrackedNames.Add(new TrackedItem(lootname, color, rarity));
			return true;

		}

		public bool UnTrack(string lootname)
		{
			lootname = lootname.Trim();

			if (lootname == "*" && TrackedNames.Count > 0)
			{
				TrackedNames.Clear();
				return true;
			}
			
			return TrackedNames.RemoveAll(t => t.Name == lootname) > 0;
		}

		public override PointOfInterest[] RefreshData()
		{
			if (TrackedNames.Count == 0)
				return Empty;

			var world = Singleton<GameWorld>.Instance;
			if (world == null)
				return Empty;

			var player = GameState.Current?.LocalPlayer;
			if (!player.IsValid())
				return Empty;

			var camera = GameState.Current?.Camera;
			if (camera == null)
				return Empty;

			var records = new List<PointOfInterest>();

			// Step 1 - look outside containers (loot items)
			FindLootItems(world, records, camera);

			// Step 2 - look inside containers (items)
			if (SearchInsideContainers)
				FindItemsInContainers(records, camera);

			return records.ToArray();
		}

		private void FindItemsInContainers(List<PointOfInterest> records, Camera camera)
		{
			var containers = FindObjectsOfType<LootableContainer>();
			foreach (var container in containers)
			{
				if (!container.IsValid())
					continue;

				var position = container.transform.position;
				FindItemsInRootItem(records, camera, container.ItemOwner?.RootItem, position);
			}
		}

		private void FindItemsInRootItem(List<PointOfInterest> records, Camera camera, Item? rootItem, Vector3 position)
		{
			var items = rootItem?
				.GetAllItems()?
				.ToArray();

			if (items == null)
				return;

			foreach (var item in items)
			{
				if (!item.IsValid())
					continue;

				if (item.IsFiltered())
					continue;

				var itemName = item.ShortName.Localized();
				if (IsTracked(itemName, item.Template.Rarity, out var color))
				{
					var owner =  item.Owner?.ContainerName?.Localized();

					records.Add(new PointOfInterest
					{
						Name = FormatName(itemName, item),
						Owner = string.Equals(itemName, owner, StringComparison.OrdinalIgnoreCase) ? null : owner,
						Position = position,
						ScreenPosition = camera.WorldPointToScreenPoint(position),
						Color = color
					});
				}
			}
		}

		private void FindLootItems(GameWorld world, List<PointOfInterest> records, Camera camera)
		{
			var lootItems = world.LootItems;
			for (var i = 0; i < lootItems.Count; i++)
			{
				var lootItem = lootItems.GetByIndex(i);
				if (!lootItem.IsValid())
					continue;

				var position = lootItem.transform.position;
				var lootItemName = lootItem.Item.ShortName.Localized();

				if (lootItem is Corpse corpse)
				{
					if (SearchInsideCorpses)
						FindItemsInRootItem(records, camera, corpse.ItemOwner?.RootItem, position);

					continue;
				}

				if (IsTracked(lootItemName, lootItem.Item.Template.Rarity, out var color))
				{
					records.Add(new PointOfInterest
					{
						Name = FormatName(lootItemName, lootItem.Item),
						Position = position,
						ScreenPosition = camera.WorldPointToScreenPoint(position),
						Color = color
					});
				}
			}
		}

		private string FormatName(string itemName, Item item)
		{
			var price = item.Template.CreditsPrice;
			if (!ShowPrices || price < 1000)
				return itemName;

			return $"{itemName} {price / 1000}K";
		}

		private bool IsTracked(string lootItemName, ELootRarity rarity, out Color color)
		{
			var result = TrackedNames.Find(t => t.Name == "*" || lootItemName.IndexOf(t.Name, StringComparison.OrdinalIgnoreCase) >= 0);
			if (result != null && RarityMatches(rarity, result.Rarity))
			{
				color = result.Color ?? Color;
				return true;
			}

			color = Color;
			return false;
		}

		private static bool RarityMatches(ELootRarity itemRarity, ELootRarity? trackedRarity)
		{
			if (!trackedRarity.HasValue)
				return true;

			return trackedRarity.Value == itemRarity;
		}
	}
}
