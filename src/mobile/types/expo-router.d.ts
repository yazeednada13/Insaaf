/* eslint-disable */
import 'expo-router';

declare module 'expo-router' {
  export namespace ExpoRouter {
    export interface __routes<T extends string | object = string> {
      hrefInputParams:
        | { pathname: import('expo-router').RelativePathString; params?: import('expo-router').UnknownInputParams }
        | { pathname: import('expo-router').ExternalPathString; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/login`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/register`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/forgot-password`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/_sitemap`; params?: import('expo-router').UnknownInputParams };
      hrefOutputParams:
        | { pathname: import('expo-router').RelativePathString; params?: import('expo-router').UnknownOutputParams }
        | { pathname: import('expo-router').ExternalPathString; params?: import('expo-router').UnknownOutputParams }
        | { pathname: `/`; params?: import('expo-router').UnknownOutputParams }
        | { pathname: `/login`; params?: import('expo-router').UnknownOutputParams }
        | { pathname: `/register`; params?: import('expo-router').UnknownOutputParams }
        | { pathname: `/forgot-password`; params?: import('expo-router').UnknownOutputParams }
        | { pathname: `/_sitemap`; params?: import('expo-router').UnknownOutputParams };
      href:
        | import('expo-router').RelativePathString
        | import('expo-router').ExternalPathString
        | `/${`?${string}` | `#${string}` | ''}`
        | `/login${`?${string}` | `#${string}` | ''}`
        | `/register${`?${string}` | `#${string}` | ''}`
        | `/forgot-password${`?${string}` | `#${string}` | ''}`
        | `/_sitemap${`?${string}` | `#${string}` | ''}`
        | { pathname: import('expo-router').RelativePathString; params?: import('expo-router').UnknownInputParams }
        | { pathname: import('expo-router').ExternalPathString; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/login`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/register`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/forgot-password`; params?: import('expo-router').UnknownInputParams }
        | { pathname: `/_sitemap`; params?: import('expo-router').UnknownInputParams };
    }
  }
}
