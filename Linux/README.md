# Simple FUSE Logger shared by Impacket SmbServer back by JSON

## FUSE Logger Setup

## Install Dependencies

### Option 1: Using venv (recommended)
```bash
# Create and activate venv
python3 -m venv .venv
source .venv/bin/activate

# Install Python packages
pip install fusepy impacket

# Install system FUSE library
sudo apt-get update
sudo apt-get install fuse libfuse-dev
```

### Option 2: System-wide install
```bash
pip install fusepy impacket --break-system-packages
sudo apt-get update
sudo apt-get install fuse libfuse-dev
```

## Usage

### 1. Create mount point and run FUSE with config
```bash
mkdir /tmp/fuselog
python3 fuse_logger.py /tmp/fuselog -c filesystem_config.json
```

Or without config (empty filesystem):
```bash
python3 fuse_logger.py /tmp/fuselog
```

### 2. Share via Impacket SMB (in another terminal)
```bash
# If using venv, activate it first
source .venv/bin/activate

# Share the mount point (single dash for flags)
python3 .venv/bin/smbserver.py -smb2support secrets /tmp/fuselog

# Or use a custom port if 445 is in use
python3 .venv/bin/smbserver.py -smb2support -port 8445 secrets /tmp/fuselog
```

### 3. Or use Responder
```bash
# Edit /etc/responder/Responder.conf to set SMB path
# Then run:
responder -I eth0 -v
```

## Config File Format

The JSON config defines the fake filesystem:

```json
{
  "files": [
    {
      "path": "/folder/file.txt",
      "content": "file contents here"
    }
  ]
}
```

Nested folders are created automatically. See `filesystem_config.json` for example.

## What it logs
- File opens (OPEN)
- Read/copy operations (COPY/READ)

Only actual file access is logged - no directory browsing noise.

## Troubleshooting

**"Address already in use"**
```bash
# Stop existing Samba
sudo systemctl stop smbd nmbd

# Or use different port
python3 .venv/bin/smbserver.py -smb2support -port 8445 secrets /tmp/fuselog
```

**"Too many open files"**
- Restart the FUSE mount
- The release() method now handles this

## Unmount
```bash
fusermount -u /tmp/fuselog
```
