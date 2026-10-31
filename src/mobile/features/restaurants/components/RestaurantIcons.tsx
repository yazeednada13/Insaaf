import Svg, { Circle, Line, Path, Polyline } from 'react-native-svg';

// Exact SVG icons from Figma Make (ExploreScreen, SearchScreen, RestaurantScreen, RestaurantCard).
// Each icon keeps the Figma viewBox; size and stroke width are passed per call site.

interface IconProps {
  size: number;
  color: string;
  strokeWidth: number;
}

export function SearchIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg fill="none" height={size} stroke={color} strokeWidth={strokeWidth} viewBox="0 0 24 24" width={size}>
      <Circle cx={11} cy={11} r={8} />
      <Line x1={21} x2={16.65} y1={21} y2={16.65} />
    </Svg>
  );
}

export function FilterIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg fill="none" height={size} stroke={color} strokeWidth={strokeWidth} viewBox="0 0 24 24" width={size}>
      <Line x1={4} x2={20} y1={6} y2={6} />
      <Line x1={8} x2={16} y1={12} y2={12} />
      <Line x1={11} x2={13} y1={18} y2={18} />
    </Svg>
  );
}

export function CloseIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg fill="none" height={size} stroke={color} strokeWidth={strokeWidth} viewBox="0 0 24 24" width={size}>
      <Line x1={18} x2={6} y1={6} y2={18} />
      <Line x1={6} x2={18} y1={6} y2={18} />
    </Svg>
  );
}

export function ChevronLeftIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg
      fill="none"
      height={size}
      stroke={color}
      strokeLinecap="round"
      strokeWidth={strokeWidth}
      viewBox="0 0 24 24"
      width={size}
    >
      <Polyline points="15 18 9 12 15 6" />
    </Svg>
  );
}

export function PhoneIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg fill="none" height={size} stroke={color} strokeWidth={strokeWidth} viewBox="0 0 24 24" width={size}>
      <Path d="M22 16.92v3a2 2 0 01-2.18 2 19.79 19.79 0 01-8.63-3.07A19.5 19.5 0 013.07 9.5a19.79 19.79 0 01-3.07-8.59A2 2 0 012 .82h3a2 2 0 012 1.72 12.84 12.84 0 00.7 2.81 2 2 0 01-.45 2.11L6.09 8.91a16 16 0 006 6l1.27-1.27a2 2 0 012.11-.45 12.84 12.84 0 002.81.7A2 2 0 0122 16.92z" />
    </Svg>
  );
}

export function MapPinIcon({ size, color, strokeWidth }: IconProps) {
  return (
    <Svg fill="none" height={size} stroke={color} strokeWidth={strokeWidth} viewBox="0 0 24 24" width={size}>
      <Path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0118 0z" />
      <Circle cx={12} cy={10} r={3} />
    </Svg>
  );
}
