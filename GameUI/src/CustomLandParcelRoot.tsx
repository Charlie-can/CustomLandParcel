import React, { useState } from "react";
import { Button } from "cs2/ui";
import { ParcelPanel } from "features/panel/ParcelPanel";
import { launcherButtonStyle } from "styles";

export function CustomLandParcelRoot(): JSX.Element {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button
        style={launcherButtonStyle}
        selected={open}
        onSelect={() => setOpen(!open)}
        tooltipLabel="Open Custom Land Parcel panel"
      >
        LP
      </Button>
      {open && <ParcelPanel onClose={() => setOpen(false)} />}
    </>
  );
}
