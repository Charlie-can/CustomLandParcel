import { TranslationKey, translations } from "i18n/translations";

export type Translator = (key: TranslationKey, replacements?: Record<string, string | number>) => string;

export function createTranslator(locale: string | undefined): Translator {
  const dictionary = (locale || "").toLowerCase().startsWith("zh") ? translations.zhHans : translations.en;
  return (key, replacements) => {
    let text = dictionary[key] || translations.en[key] || key;
    if (!replacements) {
      return text;
    }

    Object.keys(replacements).forEach((name) => {
      text = text.replace(`{${name}}`, String(replacements[name]));
    });
    return text;
  };
}
