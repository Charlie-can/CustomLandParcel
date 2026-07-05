const path = require("path");
const webpack = require("webpack");
const TerserPlugin = require("terser-webpack-plugin");

const moduleBanner = `Cities: Skylines II UI Module

Id: CustomLandParcelUI
Author: CustomLandParcel
Version: 1.0.0
Dependencies:`;

module.exports = {
  mode: "production",
  stats: "errors-warnings",
  entry: {
    CustomLandParcelUI: "./src/index.tsx",
  },
  externalsType: "window",
  externals: {
    react: "React",
    "react-dom": "ReactDOM",
    "cs2/modding": "cs2/modding",
    "cs2/api": "cs2/api",
    "cs2/bindings": "cs2/bindings",
    "cs2/l10n": "cs2/l10n",
    "cs2/ui": "cs2/ui",
    "cs2/input": "cs2/input",
    "cs2/utils": "cs2/utils",
    "cohtml/cohtml": "cohtml/cohtml",
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: {
          loader: "ts-loader",
          options: {
            transpileOnly: true,
          },
        },
        exclude: /node_modules/,
      },
      {
        test: /\.svg$/i,
        type: "asset/inline",
      },
    ],
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    modules: ["node_modules", path.join(__dirname, "src")],
  },
  output: {
    path: path.resolve(__dirname, "build"),
    filename: "[name].mjs",
    library: {
      type: "module",
    },
    publicPath: "coui://ui-mods/",
    clean: true,
  },
  optimization: {
    minimize: true,
    minimizer: [
      new TerserPlugin({
        extractComments: false,
      }),
    ],
  },
  experiments: {
    outputModule: true,
  },
  plugins: [
    new webpack.BannerPlugin({
      banner: moduleBanner,
    }),
  ],
};
