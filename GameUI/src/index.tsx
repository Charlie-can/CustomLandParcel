import { ModRegistrar } from "cs2/modding";
import { CustomLandParcelRoot } from "CustomLandParcelRoot";

const register: ModRegistrar = (moduleRegistry) => {
  console.log("[CustomLandParcelUI] registering top-left game panel launcher.");
  moduleRegistry.append("GameTopLeft", CustomLandParcelRoot);
};

export default register;
