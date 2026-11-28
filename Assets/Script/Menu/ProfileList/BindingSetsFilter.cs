using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using YARG.Localization;
using YARG.Core.Extensions;
using YARG.Helpers;

namespace YARG.Menu.ProfileList
{
    public class BindingSetsFilter : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;
        [SerializeField]
        private ProfilesMenu _profilesMenu;

        private static ControllerFamily[] _controllerFamilies = EnumExtensions<ControllerFamily>.Values;

        public void OnEnable()
        {
#if UNITY_IOS
            // YARG iOS port: label each controller type with the devices the phone detects,
            // since guitars arrive as gamepads (CRKD) or keyboards (Santroller) on iOS.
            RebuildOptions();
            InputSystem.onDeviceChange += OnDeviceChange;
#else
            if (_dropdown.options.Count is 0)
            {
                foreach (var controllerFamily in _controllerFamilies)
                {
                    _dropdown.options.Add(new(controllerFamily.ToLocalizedNamePluralTitle()));
                }
            }
#endif
        }

#if UNITY_IOS
        public void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change is InputDeviceChange.Added or InputDeviceChange.Removed)
            {
                RebuildOptions();
            }
        }

        private void RebuildOptions()
        {
            _dropdown.options.Clear();
            foreach (var controllerFamily in _controllerFamilies)
            {
                string label = controllerFamily.ToLocalizedNamePluralTitle();
                var detected = DetectedDeviceNames(controllerFamily);
                if (detected.Count > 0)
                {
                    label += $"  <color=#6fd36f>- {string.Join(", ", detected)}</color>";
                }
                _dropdown.options.Add(new(label));
            }

            _dropdown.SetValueWithoutNotify((int) _profilesMenu.CurrentBindingSetFilter);
            _dropdown.RefreshShownValue();
        }

        private static List<string> DetectedDeviceNames(ControllerFamily family)
        {
            var names = new List<string>();
            foreach (var device in InputSystem.devices)
            {
                if (device is Touchscreen or Pointer && device is not Mouse)
                {
                    continue;
                }

                if (LayoutHelper.LayoutStringToControllerFamily(device.layout) == family)
                {
                    string name = device.native ? device.displayName : $"{device.displayName} (on-screen)";
                    if (!names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }
            return names;
        }
#endif

        public void ChangeFilter()
        {
            var filter = _controllerFamilies[_dropdown.value];
            _profilesMenu.SetCurrentBindingSetFilter(filter);
        }

        public void UpdateDropdown()
        {
            _dropdown.SetValueWithoutNotify((int) _profilesMenu.CurrentBindingSetFilter);
        }
    }

    public enum ControllerFamily
    {
        FiveFretGuitar,
        SixFretGuitar,
        FourLaneDrumkit,
        FiveLaneDrumkit,
        ProKeyboard,
        MidiDevice,
        ProGuitar,
        Turntable,
        Gamepad,
        ComputerKeyboard,
        Mouse,
        Other
    }
}
