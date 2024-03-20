using System;
using System.Data;

List<string> selectedPatches = new List<string> {};
List<string> patches = new List<string> {};
List<string> patchids = new List<string> {};
List<bool> requiredpatch = new List<bool> {};
List<Action> patchfunctions = new List<Action> {};
List<string> presets = new List<string> {};
List<List<string>> presetpatches = new List<List<string>> {};

void newPatch(string id, string name, Action patch, bool required = false) {
    patches.Add(name);
    patchids.Add(id);
    patchfunctions.Add(patch);
    requiredpatch.Add(required);
}
void newPatch(string id, Action patch, bool required = false) {
    patches.Add(id);
    patchids.Add(id);
    patchfunctions.Add(patch);
    requiredpatch.Add(required);
}
bool hasPatch(string id) {
    return selectedPatches.Contains(id) || requiredpatch[patchids.IndexOf(id)];
}

// built-in patches
// TODO: separate this into its own file

List<string> GeneralHotkeyHelp = new List<string> {};
List<string> EditorHotkeyHelp = new List<string> {"F5 - Test level"};
List<string> LevelHotkeyHelp = new List<string> {"F3 - Copy level ID (user level only)", "F6 - Toggle fixed view"};

newPatch("b-req", "cirQOL", ()=>{

/// PATCHDESC: Shows the startup message
code("gml_Object_obj_notification_Create_0").AppendGML(@"
notification_set(""cirQOL "+VERSION+" loaded with "+selectedPatches.Count+(selectedPatches.Count > 1 ? " patches!" : " patch!")+(hasPatch("b-f1") ? "\nPress F1 to show the help menu" : "")+@""", 10)
", Data);

/// PATCHDESC: Initializes (and automatically defines) any global variables. Some of these only exist because I cant create new locals (hopefully lua can fix this)
code("gml_GlobalScript_0").AppendGML(@"
global.hacked_editor = true
global.show_hitboxes = false
global.make_collectables_draw_smaller = false
global.global_view_scale = 1
global.temp = 0
global.target_instance = 0
global.animationoverride = false
", Data);

/// PATCHDESC: Swaps the draw event on obj_notification from DrawGUI to DrawGUIEnd so it displays over the main menu screen
UndertaleCode obj_notification_Draw_64 = Data.GameObjects.ByName("obj_notification").EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, Data);
UndertaleCode obj_notification_Draw_75 = Data.GameObjects.ByName("obj_notification").EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUIEnd, Data);
obj_notification_Draw_75.Replace(Assembler.Assemble(Disassemble(obj_notification_Draw_64), Data));
obj_notification_Draw_64.ReplaceGML("", Data);

}, true);

newPatch("e-unclamp-setter", "Editor: Remove bounds checking for the Set... window", ()=>{
/// PATCHDESC: Removes bounds checking (for the new setter)
ReplaceTextInASM("gml_GlobalScript_legui_add_variable_update_to_window", @"push.v arg.argument0
push.v arg.argument0
pushi.e -9
push.v [stacktop]self.maxValue
push.v arg.argument0
pushi.e -9
push.v [stacktop]self.minValue
pushloc.v local.val
call.i clamp(argc=3)
dup.v 1 8 ;;; this is a weird GMS2.3+ swap instruction
dup.v 0
push.v stacktop.setVar
callv.v 1
popz.v", 

@"push.v arg.argument0
pushloc.v local.val
dup.v 1 8 ;;; this is a weird GMS2.3+ swap instruction
dup.v 0
push.v stacktop.setVar
callv.v 1
popz.v", true);
});

newPatch("e-inf-nan-and-e", "Editor: Allow values to hold infinity, NaN, and E notation", ()=>{
    
    /// PATCHDESC: Replace the real value checker to be able to use e notation, infinity, negative infinity, and NaN, allows the editor to handle such values and the setter to receive them
    code("gml_GlobalScript_is_valid_real").ReplaceGML(@"
    function is_valid_real() //gml_Script_is_valid_real
    {
        if is_real(argument0)
            return 1;
        if ((string_lower(argument0) == ""inf"") || (string_lower(argument0) == ""-inf"") || (string_lower(argument0) == ""nan""))
            return 1;
        var n = string_length(argument0)
        if ((n == 0))
            return 0;
        var c = string_char_at(argument0, 1)
        if ((c == ""+"") || (c == ""-""))
            var i = 2
        else
            i = 1
        var dot = 0
        var e = 0
        var firsttime = 1
        while ((i <= n))
        {
            c = string_char_at(argument0, i)
            if ((c == ""e""))
            {
                if (e || firsttime)
                    return 0;
                e = 1
            }
            else if ((c == "".""))
            {
                if dot
                    return 0;
                dot = 1
            }
            else
            {
                firsttime = 0
                if ((ord(c) < 48) || (ord(c) > 57))
                    return 0;
            }
            i += 1
        }
        return 1;
    }
    ", Data);
});

newPatch("e-level-shape", "Editor: Change the shape of the level with F2", ()=>{
    /// PATCHDESC: level shape hotkeys in editor
    EditorHotkeyHelp.Add("F2 - Cycle through the available level shapes (circle, square, heart) (only in editor)");
    code("gml_Object_obj_leveleditor_Step_0").AppendGML(@"
    if keyboard_check_pressed(vk_f2) {
        levelShape = levelShape + 1
        switch (levelShape) {
            case 1:
                notification_set(""Set level shape to square"", 3, -1)
            break;
            case 2:
                notification_set(""Set level shape to heart"", 3, -1)
            break;
            case 3:
                levelShape = 0
                notification_set(""Set level shape to circle"", 3, -1)
            break;
        }
    }
    ", Data);

    /// PATCHDESC: Makes the heart shape export correctly from the level editor
    ReplaceTextInASM("gml_Object_obj_le_exporter_Create_0", ":[end]", @"
    pushref.i 111
    pushi.e -9
    push.v [stacktop]self.levelShape
    pushi.e 2
    cmp.i.v EQ
    bf [9999]

    :[9998]
    push.s ""shapeHeart""
    conv.s.v
    call.i gml_Script_le_export_add(argc=1)
    popz.v

    :[9999]
    
    :[end]", true);
});

newPatch("e-control-gravity", "Editor: Toggle the control gravity gamemode with F3", ()=>{
    EditorHotkeyHelp.Add("F3 - Toggle control gravity gamemode (only in editor)");
    /// PATCHDESC: controlGravity hotkey in editor
    code("gml_Object_obj_leveleditor_Step_0").AppendGML(@"
    if keyboard_check_pressed(vk_f3)
    {
        controlGravity = !controlGravity
        if controlGravity {
            notification_set(""Set control gravity: true"", 3, -1)
        } else {
            notification_set(""Set control gravity: false"", 3, -1)
        }
    }
    ", Data);

    /// PATCHDESC: Adds controlGravity property to the level editor so it can properly be saved
    code("gml_Object_obj_leveleditor_Create_0").AppendGML("controlGravity = 0", Data);

    /// PATCHDESC: Makes controlGravity export correctly from the level editor
    ReplaceTextInASM("gml_Object_obj_le_exporter_Create_0", ":[end]", @":[99999]
    pushref.i 111
    pushi.e -9
    push.v [stacktop]self.controlGravity
    pushi.e 1
    cmp.i.v EQ
    bf [9999]

    :[9998]
    push.s ""gravcontrol""
    conv.s.v
    call.i gml_Script_le_export_add(argc=1)
    popz.v

    :[9999]
    :[end]", true);
});

newPatch("e-fixed-draw", "Editor: Holding ALT will resize collectables to be a fixed size", ()=>{
    EditorHotkeyHelp.Add("ALT - Resize collectables and portals to be a fixed size");
    /// PATCHDESC: Makes view_scale referencable
    /// TODO: Figure out which object runs this script so i can just reference view_scale directly
    ReplaceTextInGML("gml_GlobalScript_le_update_view", "}", @"
        global.global_view_scale = view_scale
    }", true);

    /// PATCHDESC: Alt hotkey for showing collectables and portals at a fixed size
    code("gml_Object_obj_leveleditor_Step_0").AppendGML(@"
    if keyboard_check(vk_alt) {
        global.make_collectables_draw_smaller = true
    } else {
        global.make_collectables_draw_smaller = false
    }
    ", Data);

    /// PATCHDESC: Makes portals draw at a fixed size if fixed drawing is enabled
    ReplaceTextInGML("gml_Object_obj_le_portal_Draw_0", "draw_sprite_stretched_ext(spr_spiral, 0, (x - 20), (y - 20), 40, 40, global.maincol, 1)", @"
    var drawsize = 40
    if global.make_collectables_draw_smaller {
        drawsize = 20 / global.global_view_scale
    }
    draw_sprite_stretched_ext(spr_spiral, 0, (x - drawsize/2), (y - drawsize/2), drawsize, drawsize, global.maincol, 1)
    ", true);

    /// PATCHDESC: Makes collectables have the fixed size in the editor when selecting
    ReplaceTextInGML("gml_GlobalScript_le_tool_collectcircle_get_radius", "switch argument0", @"
    if global.make_collectables_draw_smaller {
        return 10 / global.global_view_scale
    }
    switch argument0", true);

    /// PATCHDESC: Makes collectables have the fixed size in the editor when drawing
    ReplaceTextInGML("gml_Object_obj_le_collectcircle_Draw_0", "draw_set_color(global.maincol)", @"
    draw_set_color(global.maincol)
    if global.make_collectables_draw_smaller {
        var invert = false
        if array_contains(obj_leveleditor.selectedElements, id) {
            invert = true
            draw_set_color(c_white)
        }
        if is_trigger_instead {
            var size = 8 / global.global_view_scale;
            draw_rectangle(x - size, y - size, x + size, y + size, false)
        } else
            draw_circle_fix(x, y, 10 / global.global_view_scale, 0)
        draw_set_halign(fa_center)
        draw_set_valign(fa_middle)
        draw_set_font(fnt_small)
        if invert {
            draw_set_color(global.maincol)
        } else {
            draw_set_color(c_white)
        }
        draw_text_transformed((x - 0.5), (y - 0.5), type, 1 / global.global_view_scale, 1 / global.global_view_scale, 0)
        draw_set_halign(fa_left)
        draw_set_valign(fa_top)
        draw_set_color(global.maincol)
        return 0;
    }
    ", true);

    /// PATCHDESC: Makes collectables have the fixed size select outline in the editor when drawing
    const string drawselectcircle = "draw_circle_fix(x, y, (((type == 2) ? 32 : (iif(does_not_grow, 0.69999999999999996, 1) * 20)) + 5), 0)";
    ReplaceTextInGML("gml_Object_obj_le_collectcircle_Draw_0", drawselectcircle, @"
    if global.make_collectables_draw_smaller {
        draw_circle_fix(x, y, 10 / global.global_view_scale + 3, 0)
    } else {
        "+drawselectcircle+@"
    }
    ", true);
});

newPatch("q-show-featured", "Show if your level was featured on the 'yours' tab", ()=>{
    /// PATFCDESC: Replaces the default level status text on the 'Yours' page with "Featured" if your level got featured
ReplaceTextInASM("gml_GlobalScript_menu_customlevels_init", @":[*<A*]
pushloc.v local.thisLevel
pushi.e -9
push.v [stacktop]self.moderationStatus
pushi.e 1
cmp.i.v EQ
bf [*<C*]

:[*<B*]
push.s ""Live""@*<live*
conv.s.v
b [*<D*]", @":[*A*]
pushloc.v local.thisLevel
pushi.e -9
push.v [stacktop]self.featured
pushi.e 1
cmp.i.v EQ
bf [A]

:[*B*]
push.s  ""Featured""
conv.s.v
b [*D*]

:[A]
pushloc.v local.thisLevel
pushi.e -9
push.v [stacktop]self.moderationStatus
pushi.e 1
cmp.i.v EQ
bf [*C*]

:[B]
push.s ""Live""@*live*
conv.s.v
b [*D*]", true);
});

newPatch("q-cr", "Show clear rates above each level", ()=>{
    /// PATCHDESC: Displays a clear rate above each level if not on the "yours" tab
    /// TODO: make it display on the yours tab, currently im just replacing the unused status text which gets used on yours, so i need to come up with a better solution
    ReplaceTextInASM("gml_GlobalScript_menu_customlevels_init", @":[*<A*]
    push.i *<something*
    setowner.e
    push.s """"@***
    conv.s.v
    pushi.e -1
    pushloc.v local.i
    conv.v.i
    pop.v.v [array]self.menustatustext

    :[*<B*]", @":[*A*]
    push.i *something*
    setowner.e
    pushloc.v local.thisLevel
    pushi.e -9
    push.v [stacktop]self.plays
    pushloc.v local.thisLevel
    pushi.e -9
    push.v [stacktop]self.starts
    div.v.v
    push.i 100000
    mul.i.v
    call.i ceil(argc=1)
    pushi.e 1000
    conv.i.d
    div.d.v
    call.i string(argc=1)
    push.s  ""% CR""
    conv.s.v
    add.v.v
    pushi.e -1
    pushloc.v local.i
    conv.v.i
    pop.v.v [array]self.menustatustext
    
    :[*B*]", true);
});

newPatch("q-copy-content", "Copy the current level data with F11", ()=>{
    GeneralHotkeyHelp.Add("F11 - Copy current level data");
    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
        if keyboard_check_pressed(vk_f11) {
            clipboard_set_text(global.levelloadcontent)
            notification_set(""Copied global.levelloadcontent to clipboard"", 3, -1)
        }
    ", Data);
});

newPatch("q-load-content", "Load clipboard as level data content with F9", ()=>{
    GeneralHotkeyHelp.Add("F9 - Load level from clipboard");
    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
        if keyboard_check_pressed(vk_f9) {
            with (obj_leveleditor) {
                instance_destroy(id)
            }
            if ((global.ispaused || global.islevelend) && (global.leveltoload != -1))
                obj_gameflowhandler.prevlevel = global.leveltoload
            global.ispaused = 0
            global.islevelend = 0
            physics_pause_enable(0)
            level_start_content(clipboard_get_text(), true)
            notification_set(""Loaded level data from clipboard"", 3, -1)
        }
    ", Data);
});

newPatch("q-edit-content", "Open the current level in the editor with F12", ()=>{
    GeneralHotkeyHelp.Add("F12 - Open the current level in the level editor");
    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
        if keyboard_check_pressed(vk_f12) {
            global.levelloadcontentID = -1
            level_clear()
            instance_create(0, 0, obj_leveleditor)
            notification_set(""Entered Editor"", 3, -1)
        }
    ", Data);

    /// PATCHDESC: Makes the built-in levels openable in the level editor (the ones that were built with it anyways, only includes like half the special level pack though) and copyable via the keyboard shortcut
    ReplaceTextInGML("gml_Object_obj_createlevel_Alarm_0", "level_read(levelcode)", @"level_read(levelcode)
    global.levelloadcontent = levelcode");
});

newPatch("q-show-hitboxes", "Toggle showing hitboxes with F10", ()=>{
    GeneralHotkeyHelp.Add("F10 - Toggle show hitboxes");
    /// PATCHDESC: Changes the drawing hitboxes condition to whether hiboxes are enabled, instead of if the key combo is being held down
    ReplaceTextInGML("gml_Object_obj_renderer_Draw_73", "input_check(vk_f6) && input_check(vk_shift)", "global.show_hitboxes", true);

    /// PATCHDESC: Disables the default F10 behavior
    ReplaceTextInASM("gml_Object_obj_you_KeyPress_121", ":[0]", "exit\n:[0]", true);

    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
        if keyboard_check_pressed(vk_f10) {
            global.show_hitboxes = !global.show_hitboxes
            if global.show_hitboxes {
                notification_set(""Displaying hitboxes"", 3, -1)
            } else {
                notification_set(""Hiding hitboxes"", 3, -1)
            }
        }
    ", Data);
});

newPatch("q-animation-override-hotkey", "Toggle menu animations off or on with F8", ()=>{
    GeneralHotkeyHelp.Add("F8 - Toggle menu animations");
    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
        if keyboard_check_pressed(vk_f8) {
            global.animationoverride = 1 - global.animationoverride
            if global.animationoverride
                notification_set(""Menu animations: Off"", 5)
            else
                notification_set(""Menu animations: On"", 5)
        }
        if global.animationoverride {
            fadeout = 0
            with (obj_menu) {
                if gotoanim != 0
                    gotoanim = infinity
                if maingoto != -1
                    mainchangeprogress = 1
                menufadin = 1
            }
        }
    ", Data);

    /// PATCHDESC: Makes it so you cant hold down a button to keep selecting or moving selection, did this to ensure animation override didnt cause problems
    ReplaceTextInASM("gml_Object_obj_menu_Step_0", "input_check(", "input_check_pressed(", true);
});

newPatch("q-show-copy-tooltip", "Show a notification when pressing F3 to copy the level ID", ()=>{
    /// PATCHDESC: Adds shortcuts for the rest of the mod
    code("gml_Object_obj_renderer_Step_0").AppendGML(@"
    if keyboard_check_pressed(vk_f3) {
        with (obj_gameflowhandler) {
            if fromCustomLevelsMenu && instance_exists(obj_you)
            {
                notification_set(""Copied level ID"", 3, -1)
            }
        }
    }
    ", Data);
});

newPatch("e-idk", "Editor: I forgot what this patch does but it has to do with the player", ()=>{
    /// PATCHDESC: I dont even know what this one does
    ReplaceTextInASM("gml_Object_obj_le_edittool_Step_0", @"push.v self.type
    pushi.e 2
    cmp.i.v NEQ", @"push.v self.type
    pushi.e 9999
    cmp.i.v NEQ", true);
});

if (ENABLEOLDSETTER) newPatch("+e-oldsetter", "use old setter (DO NOT DO THIS I REPEAT DO NOT DO THIS IT WONT WORK)", ()=>{

/// PATCHDESC: Gets the most recently selected element and puts it in target_instance (for the old setter)
ReplaceTextInASM("gml_Object_obj_le_par_levelelement_Other_11", @"call.i array_push(argc=2)
popz.v", @"call.i array_push(argc=2)
popz.v
push.v self.id
pop.v.v global.target_instance", true);

/// PATCHDESC: Adds the Set... button to every button row created with this script (for the old setter)
ReplaceTextInGML("gml_GlobalScript_legui_add_variable_update_to_window", "var changeVarButtonSize = 60", @"var changeVarButtonSize = 60
        if global.hacked_editor
        {
            
            var button = legui_create_text_button(""Set..."", undefined, undefined, gml_Script_legui_update_variable, undefined, 90, """")
            ds_map_set(button, ""clickScriptArgument"", button)
            ds_map_set(button, ""for_object"", otherID)
            ds_map_set(button, ""get"", getter)
            ds_map_set(button, ""set"", setter)
            ds_map_set(button, ""addAmount"", 0)
            ds_map_set(button, ""min"", -infinity)
            ds_map_set(button, ""max"", infinity)
            ds_map_set(button, ""repeatable"", 1)
            ds_map_set(button, ""tooltip"", ""Set a specific value by typing in the number"")
            ds_map_set(button, ""promptOverride"", true)

            legui_toolbar_add_button(windowToolbar, button)
        }
", true);

/// PATCHDESC: create a function that runs the button getter when the button is passed to it (for the old setter)
code("gml_GlobalScript_cirqol_runbuttongetter").ReplaceGML("function cirqol_runbuttongetter(argument0) {}", Data);
code("gml_GlobalScript_cirqol_runbuttongetter").Replace(Assembler.Assemble(@"
:[0]
b [5]

> gml_Script_cirqol_runbuttongetter (locals=0, argc=2)
:[1]
pushglb.v global.target_instance
pushi.e -9
pushenv [4]

:[withstatement]
push.s ""set""
conv.s.v
push.v arg.argument0
call.i ds_map_find_value(argc=2)
call.i is_method(argc=1)
conv.v.b
bf [3]

:[2]
push.v arg.argument1
call.i @@This@@(argc=0)
push.s ""set""
conv.s.v
push.v arg.argument0
call.i ds_map_find_value(argc=2)
callv.v 1
popz.v
b [4]

:[3]
push.v arg.argument1
push.s ""set""
conv.s.v
push.v arg.argument0
call.i ds_map_find_value(argc=2)
call.i script_execute(argc=2)
popz.v

:[4]
popenv [withstatement]
exit.i

:[5]
push.i gml_Script_cirqol_runbuttongetter
conv.i.v
pushi.e -1
conv.i.v
call.i method(argc=2)
dup.v 0
pushi.e -1
pop.v.v [stacktop]self.cirqol_runbuttongetter
popz.v

:[end]
", Data));

/// PATCHDESC: Create a function that when run, shows the debug get string window to set values with (for the old setter)
code("gml_GlobalScript_cirqol_showsetter").ReplaceGML(@"
function cirqol_showsetter(argument0, argument1) {
    cirqol_runbuttongetter(argument1, real(get_string(""Type in a new value to be used"", argument0)))
}
", Data);

/// PATCHDESC: Create a function that when run, closes the setter window and goes back to the previous window (for the old setter)
code("gml_GlobalScript_cirqol_cancelsetter").ReplaceGML(@"
function cirqol_cancelsetter() {
    with obj_le_gui {
        legui_destroy_window()
        window = global.prevWindow
        windowRelatedTo = global.prevWindowRelatedTo
        global.prevWindow = -1
        global.prevWindowRelatedTo = undefined
    }
}
", Data);

/// PATCHDESC: Create a function that when run, closes the setter window, sets the value to what was typed in, and goes back to the previous window (for the old setter)
code("gml_GlobalScript_cirqol_confirmsetter").ReplaceGML(@"
function cirqol_confirmsetter() {
    if is_valid_real(keyboard_string) {
        with obj_le_gui {
            legui_destroy_window()
            window = global.prevWindow
            windowRelatedTo = global.prevWindowRelatedTo
            global.prevWindow = -1
            global.prevWindowRelatedTo = undefined
        }
        cirqol_runbuttongetter(global.cirqol_tempbutton, real(keyboard_string))
    } else {
        notification_set(""The provided value is not a real number"", 10, -1)
    }
}
", Data);

/// PATCHDESC: Create a function that when run, pastes the clipboard text into the input box (for the old setter)
code("gml_GlobalScript_cirqol_paste").ReplaceGML(@"
function cirqol_paste() {
    if clipboard_has_text()
        keyboard_string = clipboard_get_text()
}
", Data);

/// PATCHDESC: Create a function that when run, clears the input box (for the old setter)
code("gml_GlobalScript_cirqol_clear").ReplaceGML(@"
function cirqol_clear() {
    keyboard_string = """"
}
", Data);

/// PATCHDESC: Disable view controls while the setter window is open (for the old setter)
ReplaceTextInASM("gml_Object_obj_le_viewhandler_Step_0", @"bf [70]", @"bf [70]
pushref.i 114
pushi.e -9
push.v [stacktop]self.windowRelatedTo
push.s ""cirqol_setter""
cmp.s.v NEQ
bf [70]", true);

/// PATCHDESC: Disable button shortcuts while the setter window is open (for the old setter)
ReplaceTextInGML("gml_GlobalScript_legui_step_button", @"(obj_le_gui.windowRelatedTo != ""save"")", @"
(obj_le_gui.windowRelatedTo != ""save"") &&
(obj_le_gui.windowRelatedTo != ""cirqol_setter"")
", true);

/// PATCHDESC: Creates a new function that shows the setter window (for the old setter)
code("gml_GlobalScript_cirqol_showsetter").ReplaceGML(@"
function cirqol_showsetter(argument0, argument1) {
    with obj_le_gui {
        global.cirqol_tempbutton = argument1
        global.prevWindow = window
        global.prevWindowRelatedTo = windowRelatedTo
        window = -1
        windowRelatedTo = undefined
        if ((windowRelatedTo == ""cirqol_setter"")) {
            legui_destroy_window()
            return;
        }
        legui_create_basic_window(""Enter a new value"")
        windowRelatedTo = ""cirqol_setter""
        legui_window_add_element(legui_create_input_field("""", 320))
        keyboard_string = string(argument0)
        legui_window_add_spacing()
        global.hackedEditorWindowToolbar = legui_create_toolbar()
        legui_toolbar_add_button(global.hackedEditorWindowToolbar, legui_create_text_button(""Paste"", undefined, undefined, gml_Script_cirqol_paste, undefined, 155, ""Pastes whatever you have copied to the clipboard""))
        legui_toolbar_add_spacing_auto(global.hackedEditorWindowToolbar, 155)
        legui_toolbar_add_button(global.hackedEditorWindowToolbar, legui_create_text_button(""Set"", undefined, undefined, gml_Script_cirqol_confirmsetter, undefined, 155, """"))
        legui_window_add_element(global.hackedEditorWindowToolbar)
        legui_window_add_spacing()
        legui_window_add_element(legui_create_text_button(""Cancel"", undefined, undefined, gml_Script_cirqol_cancelsetter, undefined, 320, """"))
        legui_reposition_window()
    }
}
", Data);

/// PATCHDESC: Makes it so that the setter button actually shows the setter window instead of just adding 0 (for the old setter)
ReplaceTextInASM("gml_GlobalScript_legui_update_variable", @"pushloc.v local.newValue
push.d -0.0001
cmp.d.v GT
bf [7]", @"
push.s ""promptOverride""
conv.s.v
pushloc.v local.button
call.i ds_map_find_value(argc=2)
conv.v.b
bf [39999]

:[29999]
pushloc.v local.button
pushloc.v local.currentValue
call.i gml_Script_cirqol_showsetter(argc=2)
popz.v

:[39999]

pushloc.v local.newValue
push.d -0.0001
cmp.d.v GT
bf [7]", true);

});

newPatch("+e-flashing-underscore", "Editor: Make text inputs have a flashing underscore", ()=>{
    /// PATCHDESC: Make sure that current_time exists
    /// TODO: Do this with all newly defined variables, dont just rely on GML to auto define them
    Data.Variables.EnsureDefined("current_time", UndertaleInstruction.InstanceType.Self, true, Data.Strings, Data);

    /// PATCHDESC: Make it so the input field has a flashing underscore after it
    ReplaceTextInASM("gml_GlobalScript_legui_create_input_field", @"pushbltn.v builtin.keyboard_string
ret.v", @"pushbltn.v builtin.current_time
    pushi.e 500
    mod.i.v
    pushi.e 250
    cmp.i.v GT
    bf [399]

    :[299]
    pushbltn.v builtin.keyboard_string
    push.s ""_""
    add.s.v
    ret.v

    :[399]
    pushbltn.v builtin.keyboard_string
    ret.v", true);
});

newPatch("e-inputbox-morevals", "Editor: Allow typing E notation into the Set... window", ()=>{
    ReplaceTextInASM("gml_GlobalScript_legui_create_input_field", @"pushloc.v local.char
push.s "".""@*<refDot*
cmp.s.v NEQ
bt [*<B*]

:[*<A*]
pushloc.v local.str
push.s "".""@*>refDot*
conv.s.v
call.i string_count(argc=2)
pushi.e 1
cmp.i.v GT
b [*<C*]

:[*>B*]
push.e 1

:[*>C*]
bf [*<D*]", 
// wioth
@"
pushloc.v local.char
push.s "".""@*refDot*
cmp.s.v NEQ
bt [*B*]

:[*A*]
pushloc.v local.str
push.s "".""@*refDot*
conv.s.v
call.i string_count(argc=2)
pushi.e 1
cmp.i.v GT
b [*C*]

:[*B*]
push.e 1

:[*C*]
bf [*D*]

pushloc.v local.char
push.s ""e""
cmp.s.v NEQ
bt [B]

:[A]
pushloc.v local.str
push.s ""e""
conv.s.v
call.i string_count(argc=2)
pushi.e 1
cmp.i.v GT
b [C]

:[B]
push.e 1

:[C]
bf [*D*]
", true);
});

newPatch("e-multiple-player", "Editor: Allow placing multiple player objects", ()=>{
    /// PATCHDESC: Allows you to place more than one player into the editor
ReplaceTextInASM("gml_Object_obj_le_edittool_Step_0", @":[247]
push.v self.type
pushi.e 2", @":[247]
push.v self.type
pushi.e 9999", true);
});

newPatch("ex-q-settings", "[Experimental] Add a cirQOL button to the main menu with related settings", ()=>{

/// PATCHDESC: Makes the main menu editable via GML again, replaces the weird @@Global@@() calls with the standard global namespace
ReplaceTextInGML("gml_GlobalScript_menu_main_init", "@@Global@@()", "global", true);

/// PATCHDESC: Adds the cirQOL button to the main menu
ReplaceTextInGML("gml_GlobalScript_menu_main_init", @"menutext[curr] = ""Settings""
    menufont[curr] = fnt_biggish_semibold
    focusonmobile[curr] = 0
    curr += 1", @"

menutext[curr] = ""Settings""
menufont[curr] = fnt_biggish_semibold
focusonmobile[curr] = 0
curr += 1

menutext[curr] = ""[EXTR=version "+VERSION+@"]cirQOL""
menufont[curr] = fnt_biggish_semibold_new
focusonmobile[curr] = 0
curr += 1

", true);
ReplaceTextInGML("gml_GlobalScript_menu_main_init", "gotoanimtype[4] = 0", "gotoanimtype[4] = 0; gotoanimtype[5] = 0", true);
ReplaceTextInGML("gml_GlobalScript_menu_main_init", "mainnumber = 4", "mainnumber = 5", true);

/// PATCHDESC: Makes the menu option actually do something (currently just goes to normal settings)
ReplaceTextInGML("gml_GlobalScript_menu_main_option_handle", @"if ((main == curr))
        menu_settings_init()
    curr += 1", @"

if ((main == curr))
    menu_settings_init()
curr += 1
if ((main == curr))
    menu_settings_init()
curr += 1

", true);

});

newPatch("b-f1", "Show a help menu when you press F1", ()=>{
/// PATCHDESC: Adds the F1 menu
code("gml_Object_obj_renderer_Step_0").AppendGML(@"
if keyboard_check_pressed(vk_f1) {
    if obj_notification.notification_time == infinity
        obj_notification.notification_time = 1
    else
    notification_set("""+
        new string('\n', 50) +
        
        "General hotkeys:\\n\\n" + String.Join("\n", GeneralHotkeyHelp) + "\n\n" +
        "While playing a level:\\n\\n" + String.Join("\n", LevelHotkeyHelp) + "\n\n" +
        "While in the level editor:\\n\\n" + String.Join("\n", EditorHotkeyHelp) + "\n\n" +

        "Press F1 again to hide this popup\n" +
    
    @""", infinity)
}
", Data);
});