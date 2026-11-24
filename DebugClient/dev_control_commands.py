"""Register CLI strategies and translate their arguments into pipe requests."""

import argparse
import json
import re
import sys


class CommandStrategy:
    """Own one command's help, argument declarations and request mapping."""

    def __init__(self, name, help_text, options=(), encode=None):
        self.name = name
        self.help_text = help_text
        self.options = options
        self.encode = encode

    def register(self, subparsers):
        """Expose the command, binding its strategy to the parsed arguments."""
        parser = subparsers.add_parser(
            self.name, help=self.help_text, description=self.help_text, allow_abbrev=False
        )
        for flag, options in self.options:
            parser.add_argument(flag, **options)
        parser.set_defaults(strategy=self)

    def request(self, args):
        """Build the protocol object. Custom encoders handle nonstandard mappings."""
        if self.encode is not None:
            return self.encode(args)
        return {
            "command": self.name,
            **{key: value for key, value in vars(args).items()
               if key not in ("pid", "strategy") and value is not None},
        }


def positive_pid(value):
    try:
        pid = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError("PID must be a positive integer.") from None
    if pid <= 0:
        raise argparse.ArgumentTypeError("PID must be a positive integer.")
    return pid


def nonnegative_int(value):
    """Cursor values start at zero, so plain positivity is too strict here."""
    try:
        number = int(value)
    except ValueError:
        raise argparse.ArgumentTypeError("Expected a non-negative integer.") from None
    if number < 0:
        raise argparse.ArgumentTypeError("Expected a non-negative integer.")
    return number


def page_size(value):
    """The host keeps a bounded ring, so asking for more than it holds is a client error."""
    number = nonnegative_int(value)
    if not 1 <= number <= 512:
        raise argparse.ArgumentTypeError("Expected 1..512 records.")
    return number


def nonempty(value):
    if not value.strip():
        raise argparse.ArgumentTypeError("Expected a nonempty value.")
    return value


def mod_id(value):
    if not re.fullmatch(r"[a-zA-Z0-9_-]{1,128}", value):
        raise argparse.ArgumentTypeError("Expected a mod ID containing letters, digits, underscores or hyphens.")
    return value


def checkpoint_flag(value):
    if value.lower() == "true":
        return True
    if value.lower() == "false":
        return False
    raise argparse.ArgumentTypeError("Expected true or false.")


def assignment(value):
    name, separator, argument = value.partition("=")
    if not separator or not name or name != name.strip():
        raise argparse.ArgumentTypeError("Expected NAME=VALUE.")
    return name, argument


def json_assignment(value):
    name, argument = assignment(value)
    try:
        return name, json.loads(argument)
    except json.JSONDecodeError as error:
        raise argparse.ArgumentTypeError(f"Invalid JSON for {name}: {error.msg}") from error


def invoke_request(args):
    payload = {"command": args.command, "provider": args.provider}
    for name, value in args.arg + args.json_arg:
        if name in payload:
            raise ValueError(f"Duplicate or reserved argument: {name}")
        payload[name] = value
    return payload


def raw_request(args):
    payload = json.loads(sys.stdin.read() if args.json == "-" else args.json)
    if not isinstance(payload, dict):
        raise ValueError("Expected a JSON object.")
    return payload


MOD = ("--mod", {"required": True, "type": mod_id, "help": "Mod ID."})
CHECKPOINT = (
    "--checkpoint",
    {"type": checkpoint_flag, "nargs": "?", "const": True, "metavar": "BOOL",
     "help": "Restore the scene from a checkpoint. Accepts true or false."},
)

BUILTIN_STRATEGIES = (
    CommandStrategy("status", "Show the active game, scene, process and mods."),
    CommandStrategy("commands", "List the game's registered command strategies.", (
        ("--mod", {"type": mod_id, "help": "List commands published by this mod."}),
    )),
    CommandStrategy("errors.pull", "Read buffered warning/error records above a sequence cursor.", (
        ("--since", {"type": nonnegative_int, "metavar": "SEQ",
                     "help": "Read after this cursor; use next from the previous reply."}),
        ("--level", {"choices": ("warning", "error"), "default": "warning",
                     "help": "Lowest severity to return (default warning)."}),
        ("--max", {"type": page_size, "default": 200, "metavar": "COUNT",
                   "help": "Maximum records to return, 1..512 (default 200)."}),
    )),
    CommandStrategy("runtime.inspect", "Inspect the current game session and save state.", (
        ("--mod", {"type": nonempty, "metavar": "SLOT", "help": "Save slot ID, for example __core__."}),
    )),
    CommandStrategy("node.inspect", "Read live Godot properties and managed fields.", (
        ("--path", {"required": True, "type": nonempty, "help": "Node path from the game root."}),
        ("--field", {"action": "append", "dest": "fields", "type": nonempty, "help": "C# field to read. Repeat for more fields."}),
        ("--property", {"action": "append", "dest": "properties", "type": nonempty,
                        "help": "Godot property to read. Repeat for more properties."}),
    )),
    CommandStrategy("scene.switch", "Switch to a logical scene.", (
        ("--scene", {"required": True, "type": nonempty, "help": "Logical scene name."}),
    )),
    CommandStrategy("assets.refresh", "Refresh resource lookups and live textures.", (
        MOD,
        ("--path", {"type": nonempty, "help": "Path within the mod."}),
    )),
    CommandStrategy("mod.attach", "Load an installed folder mod into the current game.", (MOD, CHECKPOINT)),
    CommandStrategy("mod.reload", "Reload a mod and affected dependents.", (MOD, CHECKPOINT)),
    CommandStrategy("invoke", "Call a command strategy published by a mod.", (
        ("--provider", {"required": True, "type": nonempty, "help": "Mod publishing the command."}),
        ("--command", {"required": True, "type": nonempty, "help": "Published command name."}),
        ("--arg", {"action": "append", "type": assignment, "default": [], "metavar": "NAME=VALUE",
                   "help": "String argument. Repeat for more arguments."}),
        ("--json-arg", {"action": "append", "type": json_assignment, "default": [], "metavar": "NAME=JSON",
                        "help": "Typed JSON argument. Repeat for more arguments."}),
    ), encode=invoke_request),
    CommandStrategy("raw", "Send a JSON request directly.", (
        ("json", {"nargs": "?", "default": "-", "help": "JSON object, or '-' to read it from stdin."}),
    ), encode=raw_request),
)
