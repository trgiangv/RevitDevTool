import React from 'react';
import Microsoft from '@lobehub/icons/es/Microsoft';
import {FileText, FlaskConical, LayoutDashboard, Play} from 'lucide-react';
import DocSidebarItemCategoryOriginal from '@theme-original/DocSidebarItem/Category';

type Props = React.ComponentProps<typeof DocSidebarItemCategoryOriginal>;

export default function DocSidebarItemCategory(props: Props) {
  if (props.item.className?.includes('sidebar-icon--microsoft')) {
    const label = (
      <span className="sidebar-custom-label">
        <Microsoft.Color size={16} />
        <span>{props.item.label}</span>
      </span>
    );

    return (
      <DocSidebarItemCategoryOriginal
        {...props}
        item={{...props.item, label} as typeof props.item}
      />
    );
  }

  const groupIcon = props.item.className?.match(/sidebar-group--(execution|testing|logging|hosts)/)?.[1] as
    | 'execution'
    | 'testing'
    | 'logging'
    | 'hosts'
    | undefined;
  if (groupIcon) {
    const Icon = {
      execution: Play,
      testing: FlaskConical,
      logging: FileText,
      hosts: LayoutDashboard,
    }[groupIcon];

    const label = (
      <span className="sidebar-custom-label sidebar-group-label">
        <Icon size={16} strokeWidth={2.2} aria-hidden="true" />
        <span>{props.item.label}</span>
      </span>
    );

    return (
      <DocSidebarItemCategoryOriginal
        {...props}
        item={{...props.item, label} as typeof props.item}
      />
    );
  }

  return <DocSidebarItemCategoryOriginal {...props} />;
}
