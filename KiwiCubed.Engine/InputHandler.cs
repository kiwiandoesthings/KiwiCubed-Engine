namespace KiwiCubed.Engine;

using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Numerics;

struct KeyCallbackWrapper {
    public Action<Key> callback;
    public uint id;
    public string instanceID;
}

struct MouseButtonCallbackWrapper {
    public Action<MouseButton> callback;
    public uint id;
    public string instanceID;
}

struct ScrollCallbackWrapper {
    public Action<float> callback;
    public uint id;
    public string instanceID;
}

public class InputHandler : IInputHandler, IDisposable {
    private Dictionary<Key, bool> keyStates = new();
    private Dictionary<MouseButton, bool> mouseButtonStates = new();
    private Dictionary<bool, bool> scrollStates = new();

	private Dictionary<Key, List<KeyCallbackWrapper>> keyDownCallbacks = new();
	private Dictionary<Key, List<KeyCallbackWrapper>> keyUpCallbacks = new();
    private Dictionary<MouseButton, List<MouseButtonCallbackWrapper>> mouseButtonDownCallbacks = new();
	private Dictionary<MouseButton, List<MouseButtonCallbackWrapper>> mouseButtonUpCallbacks = new();
	private Dictionary<bool, List<ScrollCallbackWrapper>> scrollCallbacks = new();

    private static List<InputHandler> instances = new();

    private string instanceID;
    private uint latestID = 0;

    private IInputContext input;
    private ImGuiIOPtr io;

    public InputHandler(string id) {
        instanceID = id;

        IWindow window = MetaHandler.Get<IVirtualWindow>().GetWindow();
        input = window.CreateInput();

        foreach (IKeyboard keyboard in input.Keyboards) {
            keyboard.KeyDown += KeyDownCallbackHandler;
            keyboard.KeyUp += KeyUpCallbackHandler;
        }

        foreach (IMouse mouse in input.Mice) {
            mouse.MouseDown += MouseButtonDownCallbackHandler;
            mouse.MouseUp += MouseButtonUpCallbackHandler;
            mouse.Scroll += ScrollCallbackHandler;
        }

        instances.Add(this);

        MetaHandler.Register<IInputHandler>(this);
    }

    public void SetupImGui() {
        io = ImGui.GetIO();
    }

    public void RegisterCallbackOnKeys(List<Key> keys, Action<Key> callback, bool downOrUp) {
        foreach (Key key in keys) {
            RegisterKeyCallback(key, callback, downOrUp);
        }
    }

    public void RegisterCallbackOnMouseButtons(List<MouseButton> buttons, Action<MouseButton> callback, bool downOrUp) {
        foreach (MouseButton button in buttons) {
            RegisterMouseButtonCallback(button, callback, downOrUp);
        }
    }

    public uint RegisterKeyCallback(Key key, Action<Key> callback, bool downOrUp) {
        if (downOrUp) {
            if (!keyDownCallbacks.TryGetValue(key, out List<KeyCallbackWrapper> list)) {
                list = new List<KeyCallbackWrapper>();
                keyDownCallbacks[key] = list;
            }

            latestID += 1;
            list.Add(new KeyCallbackWrapper { callback = callback, id = latestID, instanceID = instanceID });
        } else {
			if (!keyUpCallbacks.TryGetValue(key, out List<KeyCallbackWrapper> list)) {
				list = new List<KeyCallbackWrapper>();
				keyUpCallbacks[key] = list;
			}

			latestID += 1;
			list.Add(new KeyCallbackWrapper {callback = callback, id = latestID, instanceID = instanceID});
		}

        return latestID;
    }

    public uint RegisterMouseButtonCallback(MouseButton button, Action<MouseButton> callback, bool downOrUp) {
        if (downOrUp) {
            if (!mouseButtonDownCallbacks.TryGetValue(button, out List<MouseButtonCallbackWrapper> list)) {
                list = new List<MouseButtonCallbackWrapper>();
                mouseButtonDownCallbacks[button] = list;
            }

            latestID += 1;
            list.Add(new MouseButtonCallbackWrapper { callback = callback, id = latestID, instanceID = instanceID });
        } else {
            if (!mouseButtonUpCallbacks.TryGetValue(button, out List<MouseButtonCallbackWrapper> list)) {
                list = new List<MouseButtonCallbackWrapper>();
                mouseButtonUpCallbacks[button] = list;
            }

            latestID += 1;
            list.Add(new MouseButtonCallbackWrapper { callback = callback, id = latestID, instanceID = instanceID });
        }

		return latestID;
	}

    public uint RegisterScrollCallback(bool directionY, Action<float> callback) {
        if (!scrollCallbacks.TryGetValue(directionY, out List<ScrollCallbackWrapper> list)) {
            list = new List<ScrollCallbackWrapper>();
            scrollCallbacks[directionY] = list;
        }

        latestID += 1;
        list.Add(new ScrollCallbackWrapper {callback = callback, id = latestID, instanceID = instanceID});

        return latestID;
    }

    public void DeregisterCallback(uint id, string instanceID) {
		foreach (KeyValuePair<Key, List<KeyCallbackWrapper>> pair in keyDownCallbacks) {
			for (int i = 0; i < pair.Value.Count; ++i) {
				if (pair.Value[i].id == id && pair.Value[i].instanceID == instanceID) {
					pair.Value.RemoveAt(i);
					return;
				}
			}
		}

		foreach (KeyValuePair<Key, List<KeyCallbackWrapper>> pair in keyUpCallbacks) {
            for (int i = 0; i < pair.Value.Count; ++i) {
                if (pair.Value[i].id == id && pair.Value[i].instanceID == instanceID) {
                    pair.Value.RemoveAt(i);
                    return;
                }
            }
        }

        foreach (KeyValuePair<MouseButton, List<MouseButtonCallbackWrapper>> pair in mouseButtonDownCallbacks) {
            for (int i = 0; i < pair.Value.Count; ++i) {
                if (pair.Value[i].id == id && pair.Value[i].instanceID == instanceID) {
                    pair.Value.RemoveAt(i);
                    return;
                }
            }
        }

		foreach (KeyValuePair<MouseButton, List<MouseButtonCallbackWrapper>> pair in mouseButtonUpCallbacks) {
			for (int i = 0; i < pair.Value.Count; ++i) {
				if (pair.Value[i].id == id && pair.Value[i].instanceID == instanceID) {
					pair.Value.RemoveAt(i);
					return;
				}
			}
		}

		foreach (KeyValuePair<bool, List<ScrollCallbackWrapper>> pair in scrollCallbacks) {
            for (int i = 0; i < pair.Value.Count; ++i) {
                if (pair.Value[i].id == id && pair.Value[i].instanceID == instanceID) {
                    pair.Value.RemoveAt(i);
                    return;
                }
            }
        }
    }

    public bool GetKeyState(Key key) {
        return keyStates.TryGetValue(key, out bool state) && state;
    }

    public bool GetMouseButtonState(MouseButton button) {
        return mouseButtonStates.TryGetValue(button, out bool state) && state;
    }

    public Vector2 GetMousePosition() {
        IMouse mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
        return mouse != null ? mouse.Position : Vector2.Zero;
    }

    public IKeyboard GetKeyboard() {
		IKeyboard keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
		return keyboard;
	}

    public IMouse GetMouse() {
		IMouse mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
        return mouse;
	}

    public IInputContext GetInputContext() {
        return input;
    }

    public bool SetMousePosition(Vector2 newMousePosition) {
        IMouse mouse = null;
        bool samePosition = false;
        if (input.Mice.Count > 0) {
            mouse = input.Mice[0];
            if (mouse.Position == newMousePosition) {
                samePosition = true;
            }
            mouse.Position = newMousePosition;
        }
        return samePosition;
    }

    private void KeyDownCallbackHandler(IKeyboard keyboard, Key key, int scancode) {
		//if (io.WantCaptureKeyboard) {
		//	return;
		//}

		keyStates[key] = true;

        if (keyDownCallbacks.TryGetValue(key, out List<KeyCallbackWrapper> list)) {
            foreach (KeyCallbackWrapper wrapper in list.ToList()) {
                wrapper.callback(key);
            }
        }
    }
	private void KeyUpCallbackHandler(IKeyboard keyboard, Key key, int scancode) {
		//if (io.WantCaptureKeyboard) {
		//	return;
		//}

		keyStates[key] = false;

		if (keyUpCallbacks.TryGetValue(key, out List<KeyCallbackWrapper> list)) {
			foreach (KeyCallbackWrapper wrapper in list.ToList()) {
				wrapper.callback(key);
			}
		}
	}

	private void MouseButtonDownCallbackHandler(IMouse mouse, MouseButton button) {
        if (io.WantCaptureMouse) {
            return;
        }

		mouseButtonStates[button] = true;

        if (mouseButtonDownCallbacks.TryGetValue(button, out List<MouseButtonCallbackWrapper> list)) {
            foreach (MouseButtonCallbackWrapper wrapper in list.ToList()) {
                wrapper.callback(button);
            }
        }
    }

	private void MouseButtonUpCallbackHandler(IMouse mouse, MouseButton button) {
		if (io.WantCaptureMouse) {
			return;
		}

		mouseButtonStates[button] = false;

		if (mouseButtonUpCallbacks.TryGetValue(button, out List<MouseButtonCallbackWrapper> list)) {
			foreach (MouseButtonCallbackWrapper wrapper in list.ToList()) {
				wrapper.callback(button);
			}
		}
	}

	private void ScrollCallbackHandler(IMouse mouse, ScrollWheel wheel) {
		if (io.WantCaptureMouse) {
			return;
		}

		scrollStates[false] = wheel.X > 0;
        scrollStates[true] = wheel.Y > 0;

        if (scrollCallbacks.TryGetValue(false, out List<ScrollCallbackWrapper> listX)) {
            foreach (ScrollCallbackWrapper wrapper in listX.ToList()) {
                wrapper.callback(wheel.X);
            }
        }

        if (scrollCallbacks.TryGetValue(true, out List<ScrollCallbackWrapper> listY)) {
            foreach (ScrollCallbackWrapper wrapper in listY.ToList()) {
                wrapper.callback(wheel.Y);
            }
        }
    }

    public void Dispose() {
        instances.Remove(this);

        foreach (IKeyboard keyboard in input.Keyboards) {
            keyboard.KeyDown -= KeyDownCallbackHandler;
            keyboard.KeyUp -= KeyUpCallbackHandler;
        }

        foreach (IMouse mouse in input.Mice) {
            mouse.MouseDown -= MouseButtonDownCallbackHandler;
            mouse.MouseUp -= MouseButtonUpCallbackHandler;
            mouse.Scroll -= ScrollCallbackHandler;
        }

        input.Dispose();
    }
}