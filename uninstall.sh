#!/bin/bash
# cxfiles Uninstaller
# Removes cxfiles binary
# Copyright (c) Nikolaos Protopapas. All rights reserved.
# Licensed under the MIT License.

INSTALL_DIR="$HOME/.local/bin"

echo "cxfiles Uninstaller"
echo ""

# Remove binary
if [ -f "$INSTALL_DIR/cxfiles" ]; then
    rm "$INSTALL_DIR/cxfiles"
    echo "✓ Removed $INSTALL_DIR/cxfiles"
else
    echo "  Binary not found at $INSTALL_DIR/cxfiles"
fi

# Remove uninstaller
if [ -f "$INSTALL_DIR/cxfiles-uninstall.sh" ]; then
    rm "$INSTALL_DIR/cxfiles-uninstall.sh"
fi

# Clean PATH from shell config
for RC in "$HOME/.bashrc" "$HOME/.zshrc"; do
    if [ -f "$RC" ] && grep -q "$INSTALL_DIR" "$RC" 2>/dev/null; then
        sed -i "\|$INSTALL_DIR|d" "$RC"
        echo ""
        echo "✓ Removed PATH entry from $RC"
    fi
done

echo ""
echo "✓ cxfiles uninstalled."
