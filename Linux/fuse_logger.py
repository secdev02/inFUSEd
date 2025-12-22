#!/usr/bin/env python3
"""
Minimal FUSE filesystem that logs all access attempts
Mount this, then share the mount point via Impacket/Responder SMB
"""

from fuse import FUSE, FuseOSError, Operations
import errno
from datetime import datetime
import json
import os

class LogFS(Operations):
    def __init__(self, config_file=None):
        self.files = {'/': dict(st_mode=0o755 | 0o040000, st_nlink=2)}
        self.data = {}
        
        if config_file:
            self.load_config(config_file)
    
    def load_config(self, config_file):
        with open(config_file, 'r') as f:
            config = json.load(f)
        
        for item in config.get('files', []):
            path = item['path']
            content = item.get('content', '').encode()
            
            # Create parent directories
            parts = path.split('/')[1:-1]
            for i in range(len(parts)):
                dir_path = '/' + '/'.join(parts[:i+1])
                if dir_path not in self.files:
                    self.files[dir_path] = dict(st_mode=0o755 | 0o040000, st_nlink=2)
            
            # Create file
            self.files[path] = dict(st_mode=0o644 | 0o100000, st_size=len(content))
            self.data[path] = content

    def log(self, operation, path, extra=''):
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        print(f'[{timestamp}] {operation:12} {path} {extra}')

    def getattr(self, path, fh=None):
        if path not in self.files:
            raise FuseOSError(errno.ENOENT)
        return self.files[path]

    def readdir(self, path, fh):
        entries = ['.', '..']
        if path == '/':
            # Root directory - show top-level items
            for item_path in self.files:
                if item_path != '/' and item_path.count('/') == 1:
                    entries.append(item_path[1:])
        else:
            # Subdirectory - show items in this directory
            prefix = path if path.endswith('/') else path + '/'
            for item_path in self.files:
                if item_path.startswith(prefix) and item_path != path:
                    remainder = item_path[len(prefix):]
                    if '/' not in remainder:
                        entries.append(remainder)
        return entries

    def open(self, path, flags):
        self.log('OPEN', path)
        return 0
    
    def release(self, path, fh):
        return 0

    def read(self, path, size, offset, fh):
        if offset == 0:  # Only log first read chunk to avoid spam
            self.log('COPY/READ', path)
        if path in self.data:
            return self.data[path][offset:offset + size]
        raise FuseOSError(errno.ENOENT)

    def write(self, path, data, offset, fh):
        return len(data)

    def create(self, path, mode):
        self.files[path] = dict(st_mode=mode | 0o100000, st_size=0)
        return 0

if __name__ == '__main__':
    import argparse
    
    parser = argparse.ArgumentParser(description='FUSE filesystem logger')
    parser.add_argument('mountpoint', help='Directory to mount filesystem')
    parser.add_argument('-c', '--config', help='JSON config file with filesystem structure')
    args = parser.parse_args()
    
    print('Mounting LogFS at ' + args.mountpoint)
    print('All file access will be logged below:')
    print('-' * 60)
    
    FUSE(LogFS(args.config), args.mountpoint, foreground=True, allow_other=True)
