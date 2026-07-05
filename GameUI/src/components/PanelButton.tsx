import React from "react";
import { Button } from "cs2/ui";
import { ButtonTone, getButtonStyle } from "styles";

export function PanelButton({
  children,
  disabled,
  active,
  tone,
  style,
  tooltipLabel,
  onSelect,
}: {
  children: React.ReactNode;
  disabled?: boolean;
  active?: boolean;
  tone?: ButtonTone;
  style?: React.CSSProperties;
  tooltipLabel?: string;
  onSelect: () => void;
}): JSX.Element {
  return (
    <Button
      style={getButtonStyle(tone, active, style)}
      disabled={disabled}
      selected={active}
      tooltipLabel={tooltipLabel}
      onSelect={onSelect}
    >
      {children}
    </Button>
  );
}
