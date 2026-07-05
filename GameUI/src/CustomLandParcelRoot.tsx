import React, { useState } from "react";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { activeLocaleBinding } from "bindings";
import { ParcelPanel } from "features/panel/ParcelPanel";
import { createTranslator } from "i18n";
import customLandParcelIcon from "assets/custom-land-parcel.svg";

export function CustomLandParcelRoot(): JSX.Element {
  const [open, setOpen] = useState(false);
  const locale = useValue(activeLocaleBinding);
  const t = createTranslator(locale);

  return (
    <>
      <Button
        src={customLandParcelIcon}
        variant="floating"
        selected={open}
        onSelect={() => setOpen(!open)}
        tooltipLabel={t("app.open")}
      />
      {open && <ParcelPanel t={t} onClose={() => setOpen(false)} />}
    </>
  );
}
