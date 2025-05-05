# SunderFolk Logging Tools

A BepInEx plugin that enhances SunderFolk's multiplayer connectivity by automatically replacing local IP addresses with external ones in QR codes, enabling connections from outside the local network.

## Features

- **IP Address Replacement**: Automatically detects local IP addresses in connection QR codes and replaces them with your external IP
- **QR Code Generation**: Creates scannable QR code images for easy connection sharing with mobile devices
- **Multiple IP Detection Services**: Uses multiple backup services to reliably detect your external IP address
- **Connection Debugging**: Provides detailed logging of connection data when troubleshooting is needed
- **Configuration Options**: Simple settings to control plugin behavior

## The Problem This Solves

SunderFolk's built-in QR code connection system uses your local network IP address (like 192.168.x.x), which only works for players on the same network. This plugin modifies the connection data to use your external IP address instead, enabling friends to connect from anywhere on the internet.

## Installation

1. Make sure you have [BepInEx 6](https://github.com/BepInEx/BepInEx) or later installed for SunderFolk
2. Download the latest release from the [Releases page](https://github.com/yourusername/SunderFolkLoggingTools/releases)
3. Extract the ZIP file contents to your SunderFolk installation directory, placing them in the BepInEx/plugins folder
4. Start the game

## Configuration

After running the plugin for the first time, a configuration file will be created at `BepInEx/config/SunderFolkLoggingTools.cfg`. You can edit this file to customize the plugin's behavior:

| Setting | Default | Description |
|---------|---------|-------------|
| `GenerateQrImage` | `false` | When enabled, automatically creates a QR code image file and opens it in your default image viewer when generating connection links |
| `DevMode` | `false` | Enables verbose logging of QR code data and connection details, useful for troubleshooting |

## Usage

1. Simply start a multiplayer session in SunderFolk as usual
2. When the game generates a QR code for connections, the plugin will automatically replace the local IP with your external IP
3. If QR image generation is enabled, a scannable QR code will open in your default image viewer
4. Share the QR code with friends who can now connect from anywhere!

## Port Forwarding

**Important**: For players to connect from outside your network, you'll need to set up port forwarding on your router. The plugin will detect which port SunderFolk is using and display it in the logs when DevMode is enabled.

Typical ports used are in the range of 7000-8000, 27000-28000, or 5000-6000.

## Requirements

- SunderFolk Game
- BepInEx 6 or later (.NET 6 / IL2CPP version)
- Internet connection for external IP detection
- Port forwarding configured on your router for the appropriate port

## Technical Details

This plugin works by intercepting and modifying the MessagePack-encoded data that SunderFolk uses for connection information. It:

1. Detects common local IP patterns (192.168.x.x, 10.x.x.x, and 172.16-31.x.x)
2. Queries external IP detection services to find your public IP
3. Replaces the local IP in the connection data with your external IP
4. Updates the QR code to contain the modified connection information

## Troubleshooting

If you encounter issues:

1. Enable `DevMode` in the configuration file
2. Check BepInEx logs (in BepInEx/logs) for detailed information
3. Verify that port forwarding is correctly set up on your router
4. Ensure your firewall allows SunderFolk to accept incoming connections

## Credits

- Developed by [Your Name/Username]
- Uses the [QRCoder library](https://github.com/codebude/QRCoder) for QR code generation
- Built with [BepInEx](https://github.com/BepInEx/BepInEx) plugin framework
- Special thanks to the SunderFolk community

## License

[Specify your license here, e.g., MIT, GPL, etc.]
