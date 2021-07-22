<div align="center">
	<img alt="Piglet icon" src="images/logo.png" width="200" height="200" />
	<h1>🌊 DeckSurf - The Open Stream Deck CLI & Tooling</h1>
	<p>
		<b>Lightweight and open way to manage your Stream Deck device.</b>
	</p>
	<br>
	<br>
	<br>
</div>



## How It Works

To get started, it's necessary to create a new profile, with a set of commands that will be associated with a button on the Stream Deck. To do that, you can use the `write` command in the Piglet CLI.

```bash
Usage:
  deck write [options] 

Options:
  -d, --device-index <device-index> (REQUIRED)  Index of the connected device, to which a key setting should be
                                                written. [default: -1]
  -k, --key-index <key-index> (REQUIRED)        Index of the key that needs to be written. [default: -1]
  -l, --plugin <plugin> (REQUIRED)              Plugin that contains the relevant command. [default: ]
  -c, --command <command> (REQUIRED)            Command to be executed. [default: ]
  -i, --image-path <image-path> (REQUIRED)      Path to the default image for the button. [default: ]
  -g, --action-args <action-args> (REQUIRED)    Arguments for the defined action. [default: ]
  -p, --profile <profile> (REQUIRED)            The profile to which the command should be added. [default: ]
  -?, -h, --help                                Show help and usage information
```

The following arguments are used, and are required:

| Argument                 | Description |
|:-------------------------|:------------|
| `--device-index` or `-d` | Zero-based index of the connected Stream Deck device. If only one device is connected, the index is `0`. |
| `--key-index` or `-k`    | Zero-based index of the key that is being written to. Should be within the boundaries of the keys for the connected device. |
| `--plugin` or `-l`       | The full identifier of the Piglet plugin that will be used for command handling. Should match the name of the plugin DLL, without the file extension. |
| `--command` or `-c`      | Command identifier. Should match the name of the command class in the plugin assembly. |
| `--image-path` or `-i`   | Path to the image that will be used for the button that is being written to. This can be the default image, that will be replaced later on through one of the commands. |
| `--action-args` or `-a`  | Arguments to pass to the command being executed. This string is specific to each command. |
| `--profile` or `-p`      | The name of the profile to be used. If no profile with a given name exists, a new one will be created. |

The created profile will be located in `%LOCALAPPDATA%\DenDev\{PROFILE_NAME}`. The settings are stored in a `profile.json` file within the profile folder.

## FAQ

### Why was this project created?

I was fiddling with the default Stream Deck software, and realized that it [was constantly scanning my registry and process tree](https://twitter.com/DennisCode/status/1401230392527523856). According to Elgato support, this is necessary for [Smart Profiles](https://help.elgato.com/hc/en-us/articles/360053419071-Elgato-Stream-Deck-Smart-Profiles); however, the process monitoring occurs even when Smart Profiles are not configured. While the feature itself is nice, I wasn't too comfortable with some software constantly monitoring what I run without a way to disable that, so I decided to tinker with the device and see if I can figure out how to write my own software that manages the Stream Deck device.

### Is this official/endorsed by Elgato?

No - not in any capacity. Use at your own leisure and risk.

### Where can I go to read more about the project?

This repository generally should be a good starting point, but you can also go to [https://deck.surf](https://deck.surf) for latest links and relevant information.

### Can I run this on Linux/macOS?

Not yet - I am still exploring the best way to make this tooling reliably work on Windows. Once that is done, I would love to make this also work on macOS and Linux.

### Is there a GUI management app for this?

Not yet - it's just a CLI and a .NET SDK. In the future, I expect to also create a more visual approach to managing the content, that is easier for folks that don't want to fiddle with the terminal.
