using AutoAnimalDoors.Menu;
using AutoAnimalDoors.StardewValleyWrapper;
using StardewModdingAPI.Events;
using System.Collections.Generic;
using Buildings = AutoAnimalDoors.StardewValleyWrapper.Buildings;

namespace AutoAnimalDoors
{
    class ModEntry : StardewModdingAPI.Mod
    {
        private MenuRegistry GenericMenuRegistry { get; set; }

        public override void Entry(StardewModdingAPI.IModHelper helper)
        {
            Logger.Instance.Initialize(this.Monitor);
            GenericMenuRegistry = new MenuRegistry(Helper);
            ModConfig.Instance = Helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.DayStarted += SetupAutoDoorCallbacks;
            helper.Events.GameLoop.GameLaunched += SetupMenu;
        }

        private void SetupMenu(object sender, GameLaunchedEventArgs e)
        {
            GenericMenuRegistry.InitializeMenu(ModManifest, ModConfig.Instance);
        }

        private bool IsGoToSleepDialog(StardewValley.Menus.IClickableMenu menu)
        {
            StardewValley.Menus.DialogueBox dialogBox = menu as StardewValley.Menus.DialogueBox;
            if (dialogBox != null)
            {
                List<string> dialogs = this.Helper.Reflection.GetField<List<string>>(dialogBox, "dialogues").GetValue();
                if (dialogs != null && dialogs.Count >= 1)
                {
                    return dialogs[0].Equals(StardewValley.Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep"));
                }
            }

            return false;
        }

        private void OnMenuChanged(object sender, StardewModdingAPI.Events.MenuChangedEventArgs menuChangedEventArgs)
        {
            if (IsGoToSleepDialog(menuChangedEventArgs.NewMenu))
            {
                foreach (Buildings.AnimalBuilding eligibleAnimalBuilding in this.EligibleAnimalBuildings)
                {
                    eligibleAnimalBuilding.SendAllAnimalsHome();
                }
            }
        }

        private void SetupAutoDoorCallbacks(object sender, System.EventArgs eventArgs)
        {
            // Remove callback for non host computers, no need to keep calling this
            if (!StardewModdingAPI.Context.IsOnHostComputer)
            {
                Helper.Events.GameLoop.DayStarted -= SetupAutoDoorCallbacks;
                return;
            }

            // Split screen users don't need to close the doors, but can't remove callback since the 
            // host needs to have this method run for them
            if (!StardewModdingAPI.Context.IsMainPlayer)
            {
                return;
            }

            Game game = Game.Instance;
            if (game.IsLoaded())
            {
                // Remove the subscriptions before adding them, this ensures we are only ever subscribed once
                Helper.Events.Display.MenuChanged -= this.OnMenuChanged;
                Helper.Events.GameLoop.TimeChanged -= this.OpenAnimalDoors;
                Helper.Events.GameLoop.TimeChanged -= this.CloseAnimalDoors;

                bool skipDueToWinter = !ModConfig.Instance.OpenDoorsDuringWinter && game.Season == Season.WINTER;
                bool skipDueToWeather = !ModConfig.Instance.OpenDoorsWhenRaining && (game.Weather == Weather.RAINING || game.Weather == Weather.LIGHTNING);
                if (!skipDueToWinter && !skipDueToWeather)
                {
                    if (ModConfig.Instance.AutoOpenEnabled)
                    {
                        Helper.Events.GameLoop.TimeChanged += this.OpenAnimalDoors;
                    }

                    Helper.Events.GameLoop.TimeChanged += this.CloseAnimalDoors;
                    Helper.Events.Display.MenuChanged += this.OnMenuChanged;
                }
            }
        }

        private int GetUpgradeLevelRequirementForBuidlingType(Buildings.AnimalBuildingType type)
        {
            if (type == Buildings.AnimalBuildingType.BARN)
            {
                return ModConfig.Instance.BarnRequiredUpgradeLevel;
            } else if (type == Buildings.AnimalBuildingType.COOP)
            {
                return ModConfig.Instance.CoopRequiredUpgradeLevel;
            }
            return 0;
        }

        /// <summary>This method gets only the animal buildings that are eligible for 
        ///    auto opening/closing based off the config settings.
        /// <example>For example:
        ///    If the CoopRequiredUpgradeLevel was set to 2, the Coops that are upgrade 
        ///    level 2 or higher would be returned ("Big Coop"s and "Deluxe Coop"s) while the coops
        ///    below that upgrade level (Normal "Coop"s) would not be returned.
        /// </example>
        /// </summary>
        private List<Buildings.AnimalBuilding> EligibleAnimalBuildings
        {
            get
            {
                List<Buildings.AnimalBuilding> eligibleAnimalBuildings = new List<Buildings.AnimalBuilding>(); ;
                foreach (Farm farm in Game.Instance.Farms)
                {
                    foreach (Buildings.AnimalBuilding animalBuilding in farm.AnimalBuildings)
                    {
                        if (animalBuilding.UpgradeLevel >= GetUpgradeLevelRequirementForBuidlingType(animalBuilding.Type))
                        {
                            eligibleAnimalBuildings.Add(animalBuilding);
                        }
                    }
                }
                return eligibleAnimalBuildings;
            }
        }

        private void SetAllAnimalDoorsState(Buildings.AnimalDoorState state)
        {
            foreach (Buildings.AnimalBuilding animalBuilding in this.EligibleAnimalBuildings)
            {
                animalBuilding.AnimalDoorState = state;
            }
        }

        private void CloseAnimalDoors(object sender, StardewModdingAPI.Events.TimeChangedEventArgs timeOfDayChanged)
        {
            if (timeOfDayChanged.NewTime >= ModConfig.Instance.AnimalDoorCloseTime)
            {
                List<Buildings.AnimalBuilding> eligibleAnimalBuildings = this.EligibleAnimalBuildings;
                foreach (Buildings.AnimalBuilding animalBuilding in eligibleAnimalBuildings)
                {
                    if (!animalBuilding.AreAllAnimalsHome())
                    {
                        return;
                    }
                }

                SetAllAnimalDoorsState(Buildings.AnimalDoorState.CLOSED);
                Helper.Events.GameLoop.TimeChanged -= this.CloseAnimalDoors;
            }
        }

        private void OpenAnimalDoors(object sender, StardewModdingAPI.Events.TimeChangedEventArgs timeOfDayChanged)
        {
            if (timeOfDayChanged.NewTime >= ModConfig.Instance.AnimalDoorOpenTime && timeOfDayChanged.NewTime < ModConfig.Instance.AnimalDoorCloseTime)
            {
                Helper.Events.GameLoop.TimeChanged -= this.OpenAnimalDoors;
                SetAllAnimalDoorsState(Buildings.AnimalDoorState.OPEN);
            }
        }
    }
}
